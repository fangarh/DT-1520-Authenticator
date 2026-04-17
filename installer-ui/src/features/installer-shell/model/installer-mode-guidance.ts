import type { InstallerMode } from "../../../shared/types/installer-contracts";

interface InstallerModeGuidance {
  label: string;
  description: string;
  semantics: string;
  completion: string;
}

const guidanceByMode: Record<InstallerMode, InstallerModeGuidance> = {
  Install: {
    label: "Install",
    description: "Full first-time rollout: build images, bootstrap database, create bootstrap admin and bring the runtime online.",
    semantics: "Requires a host-level env file outside git and, for a live run, a one-time bootstrap admin password passed only through process env.",
    completion: "Happy path ends when the runtime Admin UI is reachable and the operator can log in with the bootstrap admin account.",
  },
  Update: {
    label: "Update",
    description: "Controlled rollout over an existing installation: rebuild images, re-run idempotent migrations and restart runtime services.",
    semantics: "Bootstrap admin is not part of the normal update path; an occupied HTTPS port is treated as the current installation, not a failure.",
    completion: "Happy path ends when Admin UI and worker diagnostics are healthy after the runtime restart.",
  },
  Recover: {
    label: "Recover",
    description: "Fast recovery after restart or partial container failure without rebuilds or bootstrap commands.",
    semantics: "The engine only brings infrastructure and runtime services back, then rechecks runtime diagnostics and worker heartbeat.",
    completion: "Happy path ends when services recover to a healthy state and the operator has enough diagnostics for any remaining incident response.",
  },
};

export function getInstallerModeGuidance(mode: InstallerMode) {
  return guidanceByMode[mode];
}
