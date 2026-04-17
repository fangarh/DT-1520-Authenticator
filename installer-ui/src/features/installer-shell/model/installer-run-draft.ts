import type { InstallerMode, InstallerRunRequest, InstallerShellInfo } from "../../../shared/types/installer-contracts";

export const installerPermissionOptions = ["enrollments.read", "enrollments.write"] as const;

export type InstallerPermission = (typeof installerPermissionOptions)[number];

export interface InstallerRunDraft extends InstallerRunRequest {
  composeFilePath: string;
  bootstrapAdminPassword: string;
}

export function createInstallerRunDraft(shellInfo: InstallerShellInfo | null): InstallerRunDraft {
  return {
    mode: "Install",
    envFilePath: "",
    composeFilePath: shellInfo?.defaultComposeFilePath ?? "",
    bootstrapAdminUsername: "operator",
    bootstrapAdminPermissions: [...installerPermissionOptions],
    bootstrapAdminPassword: "",
    preflightOnly: false,
    dryRun: false,
    skipImageBuild: false,
    skipBootstrap: false,
    skipBootstrapAdmin: false,
    skipPortAvailabilityCheck: false,
  };
}

export function getInstallerModeLabel(mode: InstallerMode) {
  switch (mode) {
    case "Install":
      return "Install";
    case "Update":
      return "Update";
    case "Recover":
      return "Recover";
  }
}

export function shouldRequestBootstrapPassword(draft: InstallerRunDraft) {
  return draft.mode === "Install" && !draft.skipBootstrapAdmin;
}
