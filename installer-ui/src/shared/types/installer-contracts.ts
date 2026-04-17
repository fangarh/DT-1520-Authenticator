export type InstallerMode = "Install" | "Update" | "Recover";
export type InstallerOutcome = "succeeded" | "degraded" | "failed";
export type InstallerNoticeTone = "neutral" | "success" | "danger";

export interface InstallerShellInfo {
  transport: string;
  host: string;
  port: number;
  executionMode: "mock" | "live";
  defaultComposeFilePath: string;
}

export interface InstallerOperationProfile {
  mode: InstallerMode;
  displayName: string;
}

export interface InstallerManifest {
  schemaVersion: string;
  paths: {
    composeFilePath: string;
    envFilePath: string;
    reportJsonPath: string | null;
  };
  bootstrapAdmin: {
    username: string;
    permissions: string[];
  };
  options: {
    skipImageBuild: boolean;
    skipBootstrap: boolean;
    skipBootstrapAdmin: boolean;
    skipPortAvailabilityCheck: boolean;
    skipPortAvailabilityCheckWasSpecified: boolean;
    preflightOnly: boolean;
    dryRun: boolean;
  };
}

export interface InstallerConfigurationSummary {
  composeFilePath: string;
  envFilePath: string;
  envDirectoryPath: string;
  adminHttpsPort: number;
  bootstrapAdminUsername: string;
  bootstrapAdminPermissions: string[];
}

export interface InstallerValidationIssue {
  code: string;
  field: string;
  message: string;
}

export interface InstallerDiagnosticIssue {
  source: string;
  code: string;
  message: string;
}

export interface InstallerRuntimePublisher {
  url: string;
  publishedPort: number | null;
  targetPort: number | null;
  protocol: string;
}

export interface InstallerRuntimeService {
  service: string;
  state: string;
  health: string | null;
  exitCode: number | null;
  publishers: InstallerRuntimePublisher[];
}

export interface InstallerWorkerDependencyStatus {
  name: string;
  status: string;
  checkedAtUtc: string;
  failureKind: string | null;
}

export interface InstallerWorkerMetric {
  name: string;
  value: number;
}

export interface InstallerWorkerJobStatus {
  name: string;
  status: string;
  intervalSeconds: number;
  isDue: boolean;
  lastStartedAtUtc: string | null;
  lastCompletedAtUtc: string | null;
  lastSuccessfulCompletedAtUtc: string | null;
  successfulRunCount: number;
  failedRunCount: number;
  consecutiveFailureCount: number;
  lastSummary: string | null;
  failureKind: string | null;
  lastMetrics: InstallerWorkerMetric[];
}

export interface InstallerWorkerDiagnostics {
  serviceName: string;
  startedAtUtc: string;
  lastHeartbeatUtc: string;
  lastExecutionStartedUtc: string | null;
  lastExecutionCompletedUtc: string | null;
  executionOutcome: string;
  consecutiveFailureCount: number;
  dependencyStatuses: InstallerWorkerDependencyStatus[];
  jobStatuses: InstallerWorkerJobStatus[];
}

export interface InstallerTroubleshootingHint {
  code: string;
  severity: "info" | "warning" | "error";
  service: string | null;
  component: string | null;
  failureKind: string | null;
  message: string;
  recommendedAction: string;
}

export interface InstallerStepResult {
  stepId: string;
  name: string;
  status: "succeeded" | "failed";
  startedUtc: string;
  completedUtc: string;
  durationMs: number;
  message: string;
  exitCode: number;
  commandPreview: string;
  environmentOverrideKeys: string[];
  outputLines: string[];
}

export interface InstallerExecutionReport {
  schemaVersion: string;
  generatedUtc: string;
  outcome: InstallerOutcome;
  mode: InstallerMode;
  preflightOnly: boolean;
  dryRun: boolean;
  operationProfile: InstallerOperationProfile | null;
  manifest: InstallerManifest;
  configuration: InstallerConfigurationSummary | null;
  validationIssues: InstallerValidationIssue[];
  diagnosticIssues: InstallerDiagnosticIssue[];
  stepResults: InstallerStepResult[];
  runtimeStatus: {
    lines: string[];
    services: InstallerRuntimeService[];
  };
  workerDiagnostics: InstallerWorkerDiagnostics | null;
  troubleshootingHints: InstallerTroubleshootingHint[];
  summary: {
    totalSteps: number;
    succeededSteps: number;
    failedSteps: number;
    diagnosticIssueCount: number;
    troubleshootingHintCount: number;
    startedUtc: string | null;
    completedUtc: string | null;
    durationMs: number;
  };
  failureMessage: string | null;
}

export interface InstallerRunRequest {
  mode: InstallerMode;
  envFilePath: string;
  composeFilePath?: string;
  bootstrapAdminUsername: string;
  bootstrapAdminPermissions: string[];
  bootstrapAdminPassword?: string;
  preflightOnly: boolean;
  dryRun: boolean;
  skipImageBuild: boolean;
  skipBootstrap: boolean;
  skipBootstrapAdmin: boolean;
  skipPortAvailabilityCheck: boolean;
}

export interface InstallerRunResponse {
  report: InstallerExecutionReport;
}
