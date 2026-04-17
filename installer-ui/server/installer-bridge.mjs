import { createServer } from "node:http";
import { randomUUID } from "node:crypto";
import { promises as fs } from "node:fs";
import { dirname, extname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawn } from "node:child_process";
import { tmpdir } from "node:os";

const currentDirectoryPath = dirname(fileURLToPath(import.meta.url));
const packageRootPath = resolve(currentDirectoryPath, "..");
const repositoryRootPath = resolve(packageRootPath, "..");
const installScriptPath = resolve(repositoryRootPath, "infra", "scripts", "install.ps1");
const defaultComposeFilePath = resolve(repositoryRootPath, "infra", "docker-compose.yml");
const host = "127.0.0.1";
const port = 4180;
const serveDist = process.argv.includes("--serve-dist");
const mockMode = process.argv.includes("--mock");
const permissionsWhitelist = new Set(["enrollments.read", "enrollments.write"]);
let activeRunId = null;

const mimeTypes = new Map([
  [".css", "text/css; charset=utf-8"],
  [".html", "text/html; charset=utf-8"],
  [".js", "application/javascript; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".svg", "image/svg+xml"],
]);

const readRequestBody = async (request) => {
  const chunks = [];
  for await (const chunk of request) {
    chunks.push(chunk);
  }

  const bodyText = Buffer.concat(chunks).toString("utf8");
  if (!bodyText.trim()) {
    return {};
  }

  return JSON.parse(bodyText);
};

const sendJson = (response, statusCode, payload) => {
  response.writeHead(statusCode, {
    "Cache-Control": "no-store",
    "Content-Type": "application/json; charset=utf-8",
  });
  response.end(JSON.stringify(payload));
};

const toBoolean = (value) => value === true;

const buildPowerShellArguments = (requestBody, reportJsonPath) => {
  const argumentsList = [
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    installScriptPath,
    "-EnvFilePath",
    requestBody.envFilePath.trim(),
    "-Mode",
    requestBody.mode,
    "-BootstrapAdminUsername",
    requestBody.bootstrapAdminUsername.trim(),
    "-ReportJsonPath",
    reportJsonPath,
  ];

  const permissions = requestBody.bootstrapAdminPermissions.filter((permission) => permissionsWhitelist.has(permission));
  if (permissions.length > 0) {
    argumentsList.push("-BootstrapAdminPermissions", ...permissions);
  }

  if (requestBody.composeFilePath?.trim()) {
    argumentsList.push("-ComposeFilePath", requestBody.composeFilePath.trim());
  }

  if (toBoolean(requestBody.skipImageBuild)) {
    argumentsList.push("-SkipImageBuild");
  }

  if (toBoolean(requestBody.skipBootstrap)) {
    argumentsList.push("-SkipBootstrap");
  }

  if (toBoolean(requestBody.skipBootstrapAdmin)) {
    argumentsList.push("-SkipBootstrapAdmin");
  }

  if (toBoolean(requestBody.skipPortAvailabilityCheck)) {
    argumentsList.push("-SkipPortAvailabilityCheck");
  }

  if (toBoolean(requestBody.preflightOnly)) {
    argumentsList.push("-PreflightOnly");
  }

  if (toBoolean(requestBody.dryRun)) {
    argumentsList.push("-DryRun");
  }

  return argumentsList;
};

const validateRunRequest = (requestBody) => {
  const issues = [];
  const requiresLiveBootstrapPassword =
    requestBody.mode === "Install" &&
    !toBoolean(requestBody.skipBootstrapAdmin) &&
    !toBoolean(requestBody.preflightOnly) &&
    !toBoolean(requestBody.dryRun);

  if (!["Install", "Update", "Recover"].includes(requestBody.mode)) {
    issues.push("Mode must be one of Install, Update or Recover.");
  }

  if (typeof requestBody.envFilePath !== "string" || !requestBody.envFilePath.trim()) {
    issues.push("Env file path is required.");
  }

  if (typeof requestBody.bootstrapAdminUsername !== "string" || !requestBody.bootstrapAdminUsername.trim()) {
    issues.push("Bootstrap admin username is required.");
  }

  if (!Array.isArray(requestBody.bootstrapAdminPermissions) || requestBody.bootstrapAdminPermissions.length === 0) {
    issues.push("At least one bootstrap admin permission must be selected.");
  }

  if (requiresLiveBootstrapPassword && (typeof requestBody.bootstrapAdminPassword !== "string" || !requestBody.bootstrapAdminPassword.trim())) {
    issues.push("Bootstrap admin password is required for a live install that includes bootstrap admin upsert.");
  }

  return issues;
};

const createMockStepResult = (stepId, name, commandPreview, outputLines = [], startedUtc, completedUtc) => ({
  stepId,
  name,
  status: "succeeded",
  startedUtc,
  completedUtc,
  durationMs: 1000,
  message: "Step completed successfully.",
  exitCode: 0,
  commandPreview,
  environmentOverrideKeys: [],
  outputLines,
});

const createMockStepResults = (requestBody, runtimeStatusLines) => {
  if (toBoolean(requestBody.preflightOnly)) {
    return [];
  }

  const modeSteps = {
    Install: [
      ["build_images", "Build images", "docker compose build api worker admin bootstrap"],
      ["ensure_database", "Ensure database", "docker compose --profile bootstrap run --rm bootstrap ensure-database"],
      ["apply_migrations", "Apply migrations", "docker compose --profile bootstrap run --rm bootstrap migrate"],
      ["upsert_bootstrap_admin", "Upsert bootstrap admin user", "docker compose --profile bootstrap run --rm bootstrap upsert-admin-user"],
      ["start_runtime_services", "Start runtime services", "docker compose up -d --wait api admin"],
      ["start_worker", "Start worker", "docker compose up -d --wait worker"],
      ["show_runtime_status", "Show runtime status", "docker compose ps"],
    ],
    Update: [
      ["build_images", "Build images", "docker compose build api worker admin bootstrap"],
      ["ensure_database", "Ensure database", "docker compose --profile bootstrap run --rm bootstrap ensure-database"],
      ["apply_migrations", "Apply migrations", "docker compose --profile bootstrap run --rm bootstrap migrate"],
      ["start_runtime_services", "Start runtime services", "docker compose up -d --wait api admin"],
      ["start_worker", "Start worker", "docker compose up -d --wait worker"],
      ["show_runtime_status", "Show runtime status", "docker compose ps"],
    ],
    Recover: [
      ["start_infrastructure_services", "Start infrastructure services", "docker compose up -d --wait postgres redis"],
      ["start_runtime_services", "Start runtime services", "docker compose up -d --wait api admin"],
      ["start_worker", "Start worker", "docker compose up -d --wait worker"],
      ["show_runtime_status", "Show runtime status", "docker compose ps"],
    ],
  };

  return modeSteps[requestBody.mode].map(([stepId, name, commandPreview], index) => {
    const minute = 10 + index;
    return createMockStepResult(
      stepId,
      name,
      commandPreview,
      stepId === "show_runtime_status" ? runtimeStatusLines : (toBoolean(requestBody.dryRun) ? [`[dry-run] ${commandPreview}`] : []),
      `2026-04-15T18:${String(minute).padStart(2, "0")}:00Z`,
      `2026-04-15T18:${String(minute).padStart(2, "0")}:01Z`,
    );
  });
};

const createMockReport = (requestBody) => {
  const preflightOnly = toBoolean(requestBody.preflightOnly);
  const dryRun = toBoolean(requestBody.dryRun);
  const runtimeStatusLines = [
    "NAME               IMAGE               STATE      HEALTH",
    "otpauth-admin      otpauth-admin      running    healthy",
    "otpauth-api        otpauth-api        running    healthy",
    "otpauth-worker     otpauth-worker     running    healthy",
  ];
  const stepResults = createMockStepResults(requestBody, runtimeStatusLines);
  const succeededSteps = stepResults.length;

  return {
    schemaVersion: "otpauth.installer.report.v1",
    generatedUtc: "2026-04-15T18:20:00Z",
    outcome: "succeeded",
    mode: requestBody.mode,
    preflightOnly,
    dryRun,
    operationProfile: {
      mode: requestBody.mode,
      displayName: requestBody.mode.toLowerCase(),
    },
    manifest: {
      schemaVersion: "otpauth.installer.manifest.v1",
      paths: {
        composeFilePath: requestBody.composeFilePath?.trim() || defaultComposeFilePath,
        envFilePath: requestBody.envFilePath.trim(),
        reportJsonPath: "mock-report.json",
      },
      bootstrapAdmin: {
        username: requestBody.bootstrapAdminUsername.trim(),
        permissions: requestBody.bootstrapAdminPermissions,
      },
      options: {
        skipImageBuild: toBoolean(requestBody.skipImageBuild),
        skipBootstrap: toBoolean(requestBody.skipBootstrap),
        skipBootstrapAdmin: toBoolean(requestBody.skipBootstrapAdmin),
        skipPortAvailabilityCheck: toBoolean(requestBody.skipPortAvailabilityCheck),
        skipPortAvailabilityCheckWasSpecified: toBoolean(requestBody.skipPortAvailabilityCheck),
        preflightOnly,
        dryRun,
      },
    },
    configuration: {
      composeFilePath: requestBody.composeFilePath?.trim() || defaultComposeFilePath,
      envFilePath: requestBody.envFilePath.trim(),
      envDirectoryPath: "C:\\secure\\otpauth",
      adminHttpsPort: 8443,
      bootstrapAdminUsername: requestBody.bootstrapAdminUsername.trim(),
      bootstrapAdminPermissions: requestBody.bootstrapAdminPermissions,
    },
    validationIssues: [],
    diagnosticIssues: [],
    stepResults,
    runtimeStatus: {
      lines: preflightOnly ? [] : runtimeStatusLines,
      services: dryRun || preflightOnly
        ? []
        : [
            {
              service: "admin",
              state: "running",
              health: "healthy",
              exitCode: 0,
              publishers: [
                {
                  url: "https://127.0.0.1:8443/",
                  publishedPort: 8443,
                  targetPort: 443,
                  protocol: "tcp",
                },
              ],
            },
            {
              service: "api",
              state: "running",
              health: "healthy",
              exitCode: 0,
              publishers: [],
            },
            {
              service: "worker",
              state: "running",
              health: "healthy",
              exitCode: 0,
              publishers: [],
            },
          ],
    },
    workerDiagnostics: dryRun || preflightOnly
      ? null
      : {
          serviceName: "OtpAuth.Worker",
          startedAtUtc: "2026-04-15T18:18:00Z",
          lastHeartbeatUtc: "2026-04-15T18:20:00Z",
          lastExecutionStartedUtc: "2026-04-15T18:19:30Z",
          lastExecutionCompletedUtc: "2026-04-15T18:19:31Z",
          executionOutcome: "healthy",
          consecutiveFailureCount: 0,
          dependencyStatuses: [
            {
              name: "postgres",
              status: "healthy",
              checkedAtUtc: "2026-04-15T18:20:00Z",
              failureKind: null,
            },
            {
              name: "redis",
              status: "healthy",
              checkedAtUtc: "2026-04-15T18:20:00Z",
              failureKind: null,
            },
          ],
          jobStatuses: [
            {
              name: "security_data_cleanup",
              status: "healthy",
              intervalSeconds: 300,
              isDue: false,
              lastStartedAtUtc: "2026-04-15T18:19:30Z",
              lastCompletedAtUtc: "2026-04-15T18:19:31Z",
              lastSuccessfulCompletedAtUtc: "2026-04-15T18:19:31Z",
              successfulRunCount: 15,
              failedRunCount: 0,
              consecutiveFailureCount: 0,
              lastSummary: "cleanup_completed",
              failureKind: null,
              lastMetrics: [
                {
                  name: "deletedTotal",
                  value: 7,
                },
              ],
            },
          ],
        },
    troubleshootingHints: [],
    summary: {
      totalSteps: stepResults.length,
      succeededSteps,
      failedSteps: 0,
      diagnosticIssueCount: 0,
      troubleshootingHintCount: 0,
      startedUtc: stepResults[0]?.startedUtc ?? null,
      completedUtc: stepResults.at(-1)?.completedUtc ?? null,
      durationMs: stepResults.length * 1000,
    },
    failureMessage: null,
  };
};

const runInstallerCommand = async (requestBody) => {
  const reportDirectoryPath = await fs.mkdtemp(join(tmpdir(), "otpauth-installer-ui-"));
  const reportJsonPath = join(reportDirectoryPath, `${randomUUID()}.json`);
  const powerShellExecutablePath = process.platform === "win32" ? "powershell.exe" : "pwsh";
  const argumentsList = buildPowerShellArguments(requestBody, reportJsonPath);
  const childEnvironment = { ...process.env };

  if (typeof requestBody.bootstrapAdminPassword === "string" && requestBody.bootstrapAdminPassword.length > 0) {
    childEnvironment.OTPAUTH_ADMIN_PASSWORD = requestBody.bootstrapAdminPassword;
  } else {
    delete childEnvironment.OTPAUTH_ADMIN_PASSWORD;
  }

  await new Promise((resolvePromise, rejectPromise) => {
    const child = spawn(powerShellExecutablePath, argumentsList, {
      cwd: repositoryRootPath,
      env: childEnvironment,
      stdio: ["ignore", "ignore", "pipe"],
      windowsHide: true,
    });

    const stderrLines = [];
    child.stderr.setEncoding("utf8");
    child.stderr.on("data", (chunk) => {
      stderrLines.push(chunk);
    });

    child.on("error", (error) => {
      rejectPromise(error);
    });

    child.on("exit", async () => {
      try {
        const reportJson = await fs.readFile(reportJsonPath, "utf8");
        resolvePromise(JSON.parse(reportJson));
      } catch {
        rejectPromise(new Error(stderrLines.join("").trim() || "Installer report was not generated."));
      } finally {
        await fs.rm(reportDirectoryPath, { recursive: true, force: true });
      }
    });
  });
};

const serveStaticFile = async (requestPathname, response) => {
  const distRootPath = resolve(packageRootPath, "dist");
  const relativePath = requestPathname === "/" ? "index.html" : requestPathname.slice(1);
  const staticFilePath = resolve(distRootPath, relativePath);

  if (!staticFilePath.startsWith(distRootPath)) {
    response.writeHead(403);
    response.end();
    return;
  }

  try {
    const fileBuffer = await fs.readFile(staticFilePath);
    response.writeHead(200, {
      "Content-Type": mimeTypes.get(extname(staticFilePath)) ?? "application/octet-stream",
    });
    response.end(fileBuffer);
  } catch {
    const indexBuffer = await fs.readFile(resolve(distRootPath, "index.html"));
    response.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
    response.end(indexBuffer);
  }
};

const server = createServer(async (request, response) => {
  const url = new URL(request.url ?? "/", `http://${host}:${port}`);

  if (request.method === "GET" && url.pathname === "/api/shell-info") {
    sendJson(response, 200, {
      transport: "loopback-http",
      host,
      port,
      executionMode: mockMode ? "mock" : "live",
      defaultComposeFilePath,
    });
    return;
  }

  if (request.method === "POST" && url.pathname === "/api/run") {
    try {
      if (activeRunId) {
        sendJson(response, 409, {
          title: "Installer already running",
          detail: "Wait for the current local setup operation to finish before starting another run.",
        });
        return;
      }

      const requestBody = await readRequestBody(request);
      const validationIssues = validateRunRequest(requestBody);
      if (validationIssues.length > 0) {
        sendJson(response, 400, {
          title: "Invalid request",
          detail: validationIssues.join(" "),
        });
        return;
      }

      activeRunId = randomUUID();
      const report = mockMode
        ? createMockReport(requestBody)
        : await runInstallerCommand(requestBody);

      sendJson(response, 200, { report });
    } catch (error) {
      sendJson(response, 500, {
        title: "Installer bridge failed",
        detail: error instanceof Error ? error.message : "Unknown installer bridge failure.",
      });
    } finally {
      activeRunId = null;
    }
    return;
  }

  if (serveDist && request.method === "GET" && !url.pathname.startsWith("/api/")) {
    await serveStaticFile(url.pathname, response);
    return;
  }

  response.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
  response.end("Not found");
});

server.listen(port, host, () => {
  console.log(`Installer bridge listening on http://${host}:${port} (${mockMode ? "mock" : "live"})`);
});
