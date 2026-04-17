import type { InstallerRunDraft } from "./installer-run-draft";
import { shouldRequestBootstrapPassword } from "./installer-run-draft";

export interface InstallerDraftGuardrail {
  tone: "danger" | "neutral";
  title: string;
  detail: string;
}

export function getInstallerDraftGuardrails(draft: InstallerRunDraft) {
  const guardrails: InstallerDraftGuardrail[] = [];

  if (!draft.envFilePath.trim()) {
    guardrails.push({
      tone: "danger",
      title: "Env file path is required",
      detail: "Point the shell to a host-level runtime env file outside the repository before starting any mode.",
    });
  }

  if (!draft.bootstrapAdminUsername.trim()) {
    guardrails.push({
      tone: "danger",
      title: "Bootstrap admin username is required",
      detail: "The engine keeps the bootstrap admin identity in the sanitized manifest even when the mode skips admin upsert.",
    });
  }

  if (draft.bootstrapAdminPermissions.length === 0) {
    guardrails.push({
      tone: "danger",
      title: "Select at least one bootstrap permission",
      detail: "The bootstrap admin contract stays fail-closed; empty permission sets are rejected before the bridge call.",
    });
  }

  const requiresLiveBootstrapPassword =
    shouldRequestBootstrapPassword(draft) &&
    !draft.preflightOnly &&
    !draft.dryRun;

  if (requiresLiveBootstrapPassword && !draft.bootstrapAdminPassword.trim()) {
    guardrails.push({
      tone: "danger",
      title: "Bootstrap admin password is required for a live install",
      detail: "Keep the password only for the live Install path that still includes bootstrap admin upsert. Preflight and dry-run do not require it.",
    });
  }

  if (draft.preflightOnly && draft.dryRun) {
    guardrails.push({
      tone: "neutral",
      title: "Dry-run is redundant during preflight",
      detail: "Preflight already stops before execution. Leave Dry run off unless you plan to switch back to a non-preflight flow.",
    });
  }

  if (draft.mode !== "Install" && draft.skipPortAvailabilityCheck) {
    guardrails.push({
      tone: "neutral",
      title: "Port probe skip is usually unnecessary here",
      detail: "Update and Recover already allow the HTTPS port to belong to the current installation unless you explicitly override the engine behavior.",
    });
  }

  return guardrails;
}

export function hasBlockingInstallerDraftGuardrails(guardrails: InstallerDraftGuardrail[]) {
  return guardrails.some((guardrail) => guardrail.tone === "danger");
}
