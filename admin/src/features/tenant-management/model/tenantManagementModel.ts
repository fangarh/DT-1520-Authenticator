import type {
  AdminDeliveryStatusView,
  AdminDeviceOnboardingArtifactView,
  AdminDeviceOnboardingStatus,
  AdminIntegrationClientScope,
  AdminIntegrationClientView,
  AdminTenantDirectoryDetailView,
  AdminUserDeviceView,
} from "../../../shared/types/admin-contracts";
import { integrationClientScopeOptions } from "../../../shared/types/integration-client-scopes";

export type TenantManagementTab = "overview" | "apiClients" | "usersDevices" | "runtime" | "reports";

export type TenantManagementPendingAction =
  | "loadClients"
  | "createClient"
  | "rotate"
  | "scopes"
  | "deactivate"
  | "reactivate"
  | "loadDevices"
  | "createQr"
  | "revokeDevice"
  | "loadRuntime"
  | "loadReports"
  | "copy"
  | null;

export interface TenantManagementNotice {
  tone: "neutral" | "success" | "danger";
  title: string;
  detail: string;
  actionHint?: string;
}

export interface TenantClientCreateDraft {
  applicationClientId: string;
  clientId: string;
  allowedScopes: AdminIntegrationClientScope[];
}

export interface TenantUserDraft {
  externalUserId: string;
  applicationClientId: string;
  platform: "android" | "ios" | "unknown";
  ttlMinutes: string;
}

export interface TenantOneTimeSecret {
  clientId: string;
  clientSecret: string;
}

export interface TenantOneTimeQrPayload {
  activationCodeId: string;
  activationPayload: string;
  runtimeBaseUrl: string;
  expiresAtUtc: string;
}

export interface TenantReportSummary {
  devicesTotal: number;
  activeDevices: number;
  inactiveDevices: number;
  deliveriesTotal: number;
  delivered: number;
  failed: number;
  queued: number;
  callbackDeliveries: number;
  webhookDeliveries: number;
  qrArtifactsRecent: number;
  qrArtifactsIssuedInSession: number;
  qrByStatus: Record<AdminDeviceOnboardingStatus, number>;
  lastSuccessfulApprovalAtUtc: string | null;
  lastFailedApprovalAtUtc: string | null;
  lastDeviceSeenAtUtc: string | null;
}

export function createClientDraft(directory: AdminTenantDirectoryDetailView): TenantClientCreateDraft {
  return {
    applicationClientId: getDefaultApplicationClientId(directory),
    clientId: "",
    allowedScopes: ["challenges:read", "challenges:write"],
  };
}

export function createUserDraft(directory: AdminTenantDirectoryDetailView): TenantUserDraft {
  return {
    externalUserId: "",
    applicationClientId: getDefaultApplicationClientId(directory),
    platform: "android",
    ttlMinutes: "15",
  };
}

export function getDefaultApplicationClientId(directory: AdminTenantDirectoryDetailView): string {
  return directory.applications[0]?.applicationClientId
    ?? directory.integrationClients[0]?.applicationClientId
    ?? "";
}

export function sortScopes(scopes: AdminIntegrationClientScope[]): AdminIntegrationClientScope[] {
  return integrationClientScopeOptions
    .map((item) => item.value)
    .filter((scope) => scopes.includes(scope));
}

export function toggleScopeValue(
  scopes: AdminIntegrationClientScope[],
  scope: AdminIntegrationClientScope,
): AdminIntegrationClientScope[] {
  const nextScopes = scopes.includes(scope)
    ? scopes.filter((item) => item !== scope)
    : [...scopes, scope];

  return sortScopes(nextScopes);
}

export function haveSameScopes(
  left: AdminIntegrationClientScope[],
  right: AdminIntegrationClientScope[],
): boolean {
  const sortedLeft = sortScopes(left);
  const sortedRight = sortScopes(right);
  return sortedLeft.length === sortedRight.length && sortedLeft.every((scope, index) => scope === sortedRight[index]);
}

export function upsertClient(
  clients: AdminIntegrationClientView[],
  client: AdminIntegrationClientView,
): AdminIntegrationClientView[] {
  const index = clients.findIndex((item) => item.clientId === client.clientId);
  if (index < 0) return [client, ...clients];

  const next = [...clients];
  next[index] = client;
  return next;
}

export function upsertDevice(
  devices: AdminUserDeviceView[],
  device: AdminUserDeviceView,
): AdminUserDeviceView[] {
  const index = devices.findIndex((item) => item.deviceId === device.deviceId);
  if (index < 0) return [device, ...devices];

  const next = [...devices];
  next[index] = device;
  return next;
}

export function buildReportSummary(
  devices: AdminUserDeviceView[],
  deliveries: AdminDeliveryStatusView[],
  recentQrArtifacts: AdminDeviceOnboardingArtifactView[],
  issuedQrArtifacts: AdminDeviceOnboardingArtifactView[],
): TenantReportSummary {
  const qrArtifacts = mergeArtifacts(recentQrArtifacts, issuedQrArtifacts);

  return {
    devicesTotal: devices.length,
    activeDevices: devices.filter((device) => device.status === "active").length,
    inactiveDevices: devices.filter((device) => device.status !== "active").length,
    deliveriesTotal: deliveries.length,
    delivered: deliveries.filter((delivery) => delivery.status === "delivered").length,
    failed: deliveries.filter((delivery) => delivery.status === "failed").length,
    queued: deliveries.filter((delivery) => delivery.status === "queued").length,
    callbackDeliveries: deliveries.filter((delivery) => delivery.channel === "challenge_callback").length,
    webhookDeliveries: deliveries.filter((delivery) => delivery.channel === "webhook_event").length,
    qrArtifactsRecent: qrArtifacts.length,
    qrArtifactsIssuedInSession: issuedQrArtifacts.length,
    qrByStatus: {
      pending: qrArtifacts.filter((artifact) => artifact.status === "pending").length,
      consumed: qrArtifacts.filter((artifact) => artifact.status === "consumed").length,
      expired: qrArtifacts.filter((artifact) => artifact.status === "expired").length,
      revoked: qrArtifacts.filter((artifact) => artifact.status === "revoked").length,
    },
    lastSuccessfulApprovalAtUtc: latestUtc(deliveries
      .filter((delivery) => delivery.eventType === "challenge.approved" && delivery.status === "delivered")
      .map(getDeliveryActivityUtc)),
    lastFailedApprovalAtUtc: latestUtc(deliveries
      .filter((delivery) => delivery.eventType.startsWith("challenge.") && delivery.status === "failed")
      .map(getDeliveryActivityUtc)),
    lastDeviceSeenAtUtc: latestUtc(devices.map((device) => device.lastSeenAtUtc ?? device.activatedAtUtc ?? null)),
  };
}

export function parsePositiveInteger(value: string): number | null {
  const parsed = Number.parseInt(value.trim(), 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function mergeArtifacts(
  recentQrArtifacts: AdminDeviceOnboardingArtifactView[],
  issuedQrArtifacts: AdminDeviceOnboardingArtifactView[],
): AdminDeviceOnboardingArtifactView[] {
  const byId = new Map<string, AdminDeviceOnboardingArtifactView>();
  for (const artifact of recentQrArtifacts) {
    byId.set(artifact.activationCodeId, artifact);
  }
  for (const artifact of issuedQrArtifacts) {
    byId.set(artifact.activationCodeId, artifact);
  }

  return Array.from(byId.values());
}

function getDeliveryActivityUtc(delivery: AdminDeliveryStatusView): string | null {
  return delivery.deliveredAtUtc ?? delivery.lastAttemptAtUtc ?? delivery.createdAtUtc ?? null;
}

function latestUtc(values: (string | null | undefined)[]): string | null {
  return values
    .filter((value): value is string => typeof value === "string" && value.length > 0)
    .sort()
    .at(-1) ?? null;
}
