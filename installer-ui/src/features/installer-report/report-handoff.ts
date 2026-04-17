import type { InstallerExecutionReport, InstallerMode } from "../../shared/types/installer-contracts";

interface InstallerFlowOutcomeCopy {
  title: string;
  detail: string;
  nextSteps: string[];
}

const modeLabels: Record<InstallerMode, string> = {
  Install: "install",
  Update: "update",
  Recover: "recover",
};

function getModeSpecificNextSteps(mode: InstallerMode) {
  switch (mode) {
    case "Install":
      return [
        "Open the runtime Admin UI and verify that the bootstrap admin can sign in.",
        "Confirm the runtime stack is healthy: admin edge, API health route and worker heartbeat.",
        "Keep the protected env file in its host-level location and remove the one-time bootstrap password from the current shell session.",
      ];
    case "Update":
      return [
        "Recheck the Admin UI and API health endpoint after the rollout has settled.",
        "Confirm worker diagnostics are healthy and no troubleshooting hints remain unresolved.",
        "Use Recover only if the runtime needs another start attempt after the update path.",
      ];
    case "Recover":
      return [
        "Confirm the previously installed runtime services are healthy again.",
        "Use troubleshooting hints and worker diagnostics to separate runtime incidents from container startup issues.",
        "Escalate to a manual operator path only after the shell report stops giving a clean recovery route.",
      ];
  }
}

export function getAdminUiUrl(report: InstallerExecutionReport) {
  const publishedUrl = report.runtimeStatus.services
    .flatMap((service) => service.publishers)
    .find((publisher) => publisher.url.startsWith("https://"));

  if (publishedUrl) {
    return publishedUrl.url;
  }

  if (report.configuration?.adminHttpsPort) {
    return `https://127.0.0.1:${report.configuration.adminHttpsPort}/`;
  }

  return null;
}

export function getInstallerFlowOutcomeCopy(report: InstallerExecutionReport): InstallerFlowOutcomeCopy {
  const modeLabel = modeLabels[report.mode];

  if (report.preflightOnly) {
    return {
      title: "Preflight completed",
      detail: `The ${modeLabel} prerequisites passed without changing runtime state.`,
      nextSteps: [
        "Keep the same env file and mode selection.",
        "Turn off Preflight only when you are ready for the live flow.",
        "If this is a first-time rollout, ensure the bootstrap admin password is ready for the live Install path.",
      ],
    };
  }

  if (report.dryRun) {
    return {
      title: "Dry-run plan generated",
      detail: `The shell rendered the ${modeLabel} execution plan without touching runtime state.`,
      nextSteps: [
        "Review planned step ids and command previews in the sanitized report.",
        "Rerun the same mode without Dry run for the real execution path.",
        "Keep the env file and compose override stable so the live run matches the reviewed plan.",
      ],
    };
  }

  if (report.outcome === "degraded") {
    return {
      title: "Runtime needs operator follow-up",
      detail: `The ${modeLabel} flow completed, but diagnostics still report warnings that must be triaged before handoff is considered done.`,
      nextSteps: [
        "Review troubleshooting hints before reopening or handing off the runtime.",
        "Use worker diagnostics to distinguish dependency issues from job-specific incidents.",
        ...getModeSpecificNextSteps(report.mode),
      ],
    };
  }

  return {
    title: "Runtime handoff is ready",
    detail: `The ${modeLabel} flow completed with a healthy sanitized report.`,
    nextSteps: getModeSpecificNextSteps(report.mode),
  };
}
