import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { InstallerReportView } from "./InstallerReportView";
import type { InstallerExecutionReport } from "../../shared/types/installer-contracts";

const report: InstallerExecutionReport = {
  schemaVersion: "otpauth.installer.report.v1",
  generatedUtc: "2026-04-15T18:00:00Z",
  outcome: "succeeded",
  mode: "Install",
  preflightOnly: false,
  dryRun: false,
  operationProfile: {
    mode: "Install",
    displayName: "install",
  },
  manifest: {
    schemaVersion: "otpauth.installer.manifest.v1",
    paths: {
      composeFilePath: "D:\\Projects\\2026\\DT-1520-Authenticator\\infra\\docker-compose.yml",
      envFilePath: "C:\\secure\\otpauth\\runtime.env",
      reportJsonPath: "C:\\secure\\otpauth\\report.json",
    },
    bootstrapAdmin: {
      username: "operator",
      permissions: ["enrollments.read", "enrollments.write"],
    },
    options: {
      skipImageBuild: false,
      skipBootstrap: false,
      skipBootstrapAdmin: false,
      skipPortAvailabilityCheck: false,
      skipPortAvailabilityCheckWasSpecified: false,
      preflightOnly: false,
      dryRun: false,
    },
  },
  configuration: {
    composeFilePath: "D:\\Projects\\2026\\DT-1520-Authenticator\\infra\\docker-compose.yml",
    envFilePath: "C:\\secure\\otpauth\\runtime.env",
    envDirectoryPath: "C:\\secure\\otpauth",
    adminHttpsPort: 8443,
    bootstrapAdminUsername: "operator",
    bootstrapAdminPermissions: ["enrollments.read", "enrollments.write"],
  },
  validationIssues: [
    {
      code: "env_key_missing",
      field: "ConnectionStrings__Postgres",
      message: "Env file must contain a non-empty connection string.",
    },
  ],
  diagnosticIssues: [],
  stepResults: [
    {
      stepId: "build_images",
      name: "Build images",
      status: "succeeded",
      startedUtc: "2026-04-15T18:00:00Z",
      completedUtc: "2026-04-15T18:00:05Z",
      durationMs: 5000,
      message: "Step completed successfully.",
      exitCode: 0,
      commandPreview: "docker compose build api worker admin bootstrap",
      environmentOverrideKeys: [],
      outputLines: [],
    },
  ],
  runtimeStatus: {
    lines: [],
    services: [
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
        service: "worker",
        state: "running",
        health: "healthy",
        exitCode: 0,
        publishers: [],
      },
    ],
  },
  workerDiagnostics: null,
  troubleshootingHints: [],
  summary: {
    totalSteps: 1,
    succeededSteps: 1,
    failedSteps: 0,
    diagnosticIssueCount: 0,
    troubleshootingHintCount: 0,
    startedUtc: "2026-04-15T18:00:00Z",
    completedUtc: "2026-04-15T18:00:05Z",
    durationMs: 5000,
  },
  failureMessage: null,
};

describe("InstallerReportView", () => {
  it("renders validation issues and operational handoff details", () => {
    render(<InstallerReportView report={report} />);

    expect(screen.getByText("Validation issues")).toBeTruthy();
    expect(screen.getByText(/ConnectionStrings__Postgres/i)).toBeTruthy();
    const link = screen.getByRole("link", { name: "Open local Admin UI" }) as HTMLAnchorElement;

    expect(link.getAttribute("href")).toBe("https://127.0.0.1:8443/");
    expect(screen.getByText(/runtime handoff is ready/i)).toBeTruthy();
  });
});
