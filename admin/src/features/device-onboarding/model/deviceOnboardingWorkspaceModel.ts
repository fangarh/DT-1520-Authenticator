import type {
  AdminDeviceOnboardingArtifactView,
  AdminDeviceOnboardingPlatform,
  AdminDeviceOnboardingStatus,
} from "../../../shared/types/admin-contracts";

export interface DeviceOnboardingWorkspaceNotice {
  tone: "neutral" | "success" | "danger";
  title: string;
  detail: string;
  actionHint?: string;
}

export type DeviceOnboardingPendingAction = "load" | "create" | "revoke" | "copy" | null;

export interface LookupDraft {
  tenantId: string;
  externalUserId: string;
  applicationClientId: string;
  status: AdminDeviceOnboardingStatus | "";
  limit: string;
}

export interface CreateDraft {
  tenantId: string;
  applicationClientId: string;
  externalUserId: string;
  platform: AdminDeviceOnboardingPlatform;
  ttlMinutes: string;
}

export interface OneTimeActivationPayload {
  activationCodeId: string;
  activationPayload: string;
  expiresAtUtc: string;
}

export function createLookupDraft(): LookupDraft {
  return {
    tenantId: "",
    externalUserId: "",
    applicationClientId: "",
    status: "pending",
    limit: "50",
  };
}

export function createArtifactDraft(tenantId = ""): CreateDraft {
  return {
    tenantId,
    applicationClientId: "",
    externalUserId: "",
    platform: "android",
    ttlMinutes: "15",
  };
}

export function parsePositiveInteger(value: string): number | null {
  const parsed = Number.parseInt(value.trim(), 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

export function upsertArtifact(
  artifacts: AdminDeviceOnboardingArtifactView[],
  artifact: AdminDeviceOnboardingArtifactView,
): AdminDeviceOnboardingArtifactView[] {
  const index = artifacts.findIndex((item) => item.activationCodeId === artifact.activationCodeId);
  if (index < 0) {
    return [artifact, ...artifacts];
  }

  const next = [...artifacts];
  next[index] = artifact;
  return next;
}
