const base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

export type EnrollmentStatus = "pending" | "confirmed" | "revoked";

export interface ArtifactRecord {
  secret: string;
  secretUri: string;
  qrCodePayload: string;
}

export interface EnrollmentRecord {
  enrollmentId: string;
  tenantId: string;
  applicationClientId: string;
  externalUserId: string;
  label: string;
  status: EnrollmentStatus;
  hasPendingReplacement: boolean;
  confirmedAtUtc: string | null;
  revokedAtUtc: string | null;
}

export interface WebhookSubscriptionRecord {
  subscriptionId: string;
  tenantId: string;
  applicationClientId: string;
  endpointUrl: string;
  status: "active" | "inactive";
  eventTypes: string[];
  createdUtc: string;
  updatedUtc: string | null;
}

export interface DeliveryStatusRecord {
  deliveryId: string;
  tenantId: string;
  applicationClientId: string;
  channel: "challenge_callback" | "webhook_event";
  status: "queued" | "delivered" | "failed";
  eventType: string;
  deliveryDestination: string;
  subjectType: string;
  subjectId: string;
  publicationId?: string | null;
  attemptCount: number;
  occurredAtUtc: string;
  createdAtUtc: string;
  nextAttemptAtUtc: string;
  lastAttemptAtUtc?: string | null;
  deliveredAtUtc?: string | null;
  lastErrorCode?: string | null;
  isRetryScheduled: boolean;
}

export interface UserDeviceRecord {
  deviceId: string;
  tenantId: string;
  externalUserId: string;
  platform: "android" | "ios" | "unknown";
  status: "active" | "revoked" | "blocked";
  isPushCapable: boolean;
  activatedAtUtc: string | null;
  lastSeenAtUtc: string | null;
  revokedAtUtc: string | null;
  blockedAtUtc: string | null;
}

export interface DeviceOnboardingArtifactRecord {
  activationCodeId: string;
  tenantId: string;
  applicationClientId: string;
  externalUserId: string;
  platform: "android" | "ios" | "unknown";
  status: "pending" | "consumed" | "expired" | "revoked";
  expiresAtUtc: string;
  consumedAtUtc: string | null;
  revokedAtUtc: string | null;
  createdAtUtc: string;
  activationPayload: string;
}

export interface IntegrationClientRecord {
  clientId: string;
  tenantId: string;
  applicationClientId: string;
  status: "active" | "inactive";
  allowedScopes: string[];
  createdUtc: string;
  updatedUtc: string | null;
  lastSecretRotatedUtc: string | null;
  lastAuthStateChangedUtc: string;
}

export type TenantDirectoryStatus = "active" | "disabled" | "archived" | "test";

export interface TenantDirectoryTenantRecord {
  tenantId: string;
  displayName: string;
  slug: string | null;
  status: TenantDirectoryStatus;
  applicationCount: number;
  integrationClientCount: number;
  createdUtc: string;
  updatedUtc: string | null;
}

export interface TenantDirectoryApplicationRecord {
  applicationClientId: string;
  tenantId: string;
  displayName: string;
  slug: string | null;
  status: TenantDirectoryStatus;
  integrationClientCount: number;
  createdUtc: string;
  updatedUtc: string | null;
}

export function createArtifact(issuer: string, label: string, suffix: string): ArtifactRecord {
  const secret = createBase32Secret(suffix);
  const secretUri = `otpauth://totp/${encodeURIComponent(`${issuer}:${label}`)}?secret=${secret}&issuer=${encodeURIComponent(issuer)}&algorithm=SHA1&digits=6&period=30`;
  return {
    secret,
    secretUri,
    qrCodePayload: secretUri,
  };
}

export function createSeedDeliveryStatuses(tenantId: string, applicationClientId: string): DeliveryStatusRecord[] {
  return [
    {
      deliveryId: "delivery-failed-webhook",
      tenantId,
      applicationClientId,
      channel: "webhook_event",
      status: "failed",
      eventType: "device.blocked",
      deliveryDestination: "https://crm.example.com/webhooks/platform",
      subjectType: "device",
      subjectId: "device-1",
      publicationId: "publication-1",
      attemptCount: 3,
      occurredAtUtc: "2026-04-20T10:00:00Z",
      createdAtUtc: "2026-04-20T10:00:05Z",
      nextAttemptAtUtc: "2026-04-20T10:05:00Z",
      lastAttemptAtUtc: "2026-04-20T10:04:00Z",
      deliveredAtUtc: null,
      lastErrorCode: "delivery_failed",
      isRetryScheduled: true,
    },
    {
      deliveryId: "delivery-delivered-callback",
      tenantId,
      applicationClientId,
      channel: "challenge_callback",
      status: "delivered",
      eventType: "challenge.approved",
      deliveryDestination: "https://crm.example.com/callbacks/challenge",
      subjectType: "challenge",
      subjectId: "challenge-1",
      publicationId: null,
      attemptCount: 1,
      occurredAtUtc: "2026-04-20T09:55:00Z",
      createdAtUtc: "2026-04-20T09:55:02Z",
      nextAttemptAtUtc: "2026-04-20T09:55:02Z",
      lastAttemptAtUtc: "2026-04-20T09:55:02Z",
      deliveredAtUtc: "2026-04-20T09:55:02Z",
      lastErrorCode: null,
      isRetryScheduled: false,
    },
    {
      deliveryId: "delivery-queued-webhook",
      tenantId,
      applicationClientId,
      channel: "webhook_event",
      status: "queued",
      eventType: "factor.revoked",
      deliveryDestination: "https://erp.example.com/webhooks/security",
      subjectType: "factor",
      subjectId: "factor-1",
      publicationId: "publication-2",
      attemptCount: 0,
      occurredAtUtc: "2026-04-20T10:10:00Z",
      createdAtUtc: "2026-04-20T10:10:01Z",
      nextAttemptAtUtc: "2026-04-20T10:10:30Z",
      lastAttemptAtUtc: null,
      deliveredAtUtc: null,
      lastErrorCode: null,
      isRetryScheduled: false,
    },
  ];
}

export function createSeedUserDevices(tenantId: string, externalUserId: string): UserDeviceRecord[] {
  return [
    {
      deviceId: "11111111-1111-1111-1111-111111111111",
      tenantId,
      externalUserId,
      platform: "android",
      status: "active",
      isPushCapable: true,
      activatedAtUtc: "2026-04-20T09:00:00Z",
      lastSeenAtUtc: "2026-04-20T10:12:00Z",
      revokedAtUtc: null,
      blockedAtUtc: null,
    },
    {
      deviceId: "22222222-2222-2222-2222-222222222222",
      tenantId,
      externalUserId,
      platform: "android",
      status: "blocked",
      isPushCapable: true,
      activatedAtUtc: "2026-04-19T09:00:00Z",
      lastSeenAtUtc: "2026-04-19T11:30:00Z",
      revokedAtUtc: null,
      blockedAtUtc: "2026-04-19T11:31:00Z",
    },
    {
      deviceId: "33333333-3333-3333-3333-333333333333",
      tenantId,
      externalUserId,
      platform: "ios",
      status: "revoked",
      isPushCapable: false,
      activatedAtUtc: "2026-04-18T08:00:00Z",
      lastSeenAtUtc: "2026-04-18T09:45:00Z",
      revokedAtUtc: "2026-04-18T10:00:00Z",
      blockedAtUtc: null,
    },
  ];
}

export function createSeedDeviceOnboardingArtifacts(
  tenantId: string,
  applicationClientId: string,
  externalUserId: string,
): DeviceOnboardingArtifactRecord[] {
  return [
    {
      activationCodeId: "44444444-4444-4444-4444-444444444444",
      tenantId,
      applicationClientId,
      externalUserId,
      platform: "android",
      status: "pending",
      expiresAtUtc: "2026-04-27T10:30:00Z",
      consumedAtUtc: null,
      revokedAtUtc: null,
      createdAtUtc: "2026-04-27T10:00:00Z",
      activationPayload: "fixture-existing-payload-hidden-from-list",
    },
  ];
}

export function createSeedIntegrationClients(tenantId: string, applicationClientId: string): IntegrationClientRecord[] {
  return [
    {
      clientId: "project-manager-pilot",
      tenantId,
      applicationClientId,
      status: "active",
      allowedScopes: ["challenges:read", "challenges:write"],
      createdUtc: "2026-04-27T09:00:00Z",
      updatedUtc: null,
      lastSecretRotatedUtc: null,
      lastAuthStateChangedUtc: "2026-04-27T09:00:00Z",
    },
  ];
}

export function createSeedTenantDirectory(
  tenantId: string,
  applicationClientId: string,
): { tenants: TenantDirectoryTenantRecord[]; applications: TenantDirectoryApplicationRecord[] } {
  return {
    tenants: [
      {
        tenantId,
        displayName: "Directory Tenant",
        slug: "directory-tenant",
        status: "active",
        applicationCount: 1,
        integrationClientCount: 1,
        createdUtc: "2026-04-28T09:00:00Z",
        updatedUtc: "2026-04-28T09:30:00Z",
      },
    ],
    applications: [
      {
        applicationClientId,
        tenantId,
        displayName: "Project Manager",
        slug: "project-manager",
        status: "active",
        integrationClientCount: 1,
        createdUtc: "2026-04-28T09:00:00Z",
        updatedUtc: "2026-04-28T09:30:00Z",
      },
    ],
  };
}

export function listDeliveryStatuses(
  deliveries: DeliveryStatusRecord[],
  tenantId: string,
  searchParams: URLSearchParams,
): DeliveryStatusRecord[] {
  const applicationClientId = searchParams.get("applicationClientId");
  const channel = searchParams.get("channel");
  const status = searchParams.get("status");
  const limit = Number.parseInt(searchParams.get("limit") ?? `${deliveries.length}`, 10);

  return deliveries
    .filter((delivery) => delivery.tenantId === tenantId)
    .filter((delivery) => !applicationClientId || delivery.applicationClientId === applicationClientId)
    .filter((delivery) => !channel || delivery.channel === channel)
    .filter((delivery) => !status || delivery.status === status)
    .slice(0, Number.isFinite(limit) ? limit : deliveries.length);
}

export function listUserDevices(
  devices: UserDeviceRecord[],
  tenantId: string,
  externalUserId: string,
): UserDeviceRecord[] {
  return devices.filter((device) => (
    device.tenantId === tenantId &&
    device.externalUserId === externalUserId
  ));
}

export function revokeUserDevice(
  devices: UserDeviceRecord[],
  tenantId: string,
  externalUserId: string,
  deviceId: string,
): { devices: UserDeviceRecord[]; revokedDevice?: UserDeviceRecord; errorStatus?: number; errorTitle?: string; errorDetail?: string } {
  const index = devices.findIndex((device) => (
    device.tenantId === tenantId &&
    device.externalUserId === externalUserId &&
    device.deviceId === deviceId
  ));

  if (index < 0) {
    return {
      devices,
      errorStatus: 404,
      errorTitle: "Device not found.",
      errorDetail: "Device does not belong to the requested tenant/external user scope.",
    };
  }

  const current = devices[index]!;
  if (current.status !== "active") {
    return {
      devices,
      errorStatus: 409,
      errorTitle: "Device cannot be revoked.",
      errorDetail: "Only active devices can be revoked.",
    };
  }

  const revokedDevice: UserDeviceRecord = {
    ...current,
    status: "revoked",
    revokedAtUtc: "2026-04-20T10:20:00Z",
    lastSeenAtUtc: current.lastSeenAtUtc ?? "2026-04-20T10:20:00Z",
  };
  const nextDevices = [...devices];
  nextDevices[index] = revokedDevice;

  return {
    devices: nextDevices,
    revokedDevice,
  };
}

export function listDeviceOnboardingArtifacts(
  artifacts: DeviceOnboardingArtifactRecord[],
  tenantId: string,
  searchParams: URLSearchParams,
): Omit<DeviceOnboardingArtifactRecord, "activationPayload">[] {
  const externalUserId = searchParams.get("externalUserId");
  const applicationClientId = searchParams.get("applicationClientId");
  const status = searchParams.get("status");
  const limit = Number.parseInt(searchParams.get("limit") ?? `${artifacts.length}`, 10);

  return artifacts
    .filter((artifact) => artifact.tenantId === tenantId)
    .filter((artifact) => !externalUserId || artifact.externalUserId === externalUserId)
    .filter((artifact) => !applicationClientId || artifact.applicationClientId === applicationClientId)
    .filter((artifact) => !status || artifact.status === status)
    .slice(0, Number.isFinite(limit) ? limit : artifacts.length)
    .map(toDeviceOnboardingArtifactResponse);
}

export function createDeviceOnboardingArtifact(
  artifacts: DeviceOnboardingArtifactRecord[],
  payload: {
    tenantId: string;
    applicationClientId: string;
    externalUserId: string;
    platform: "android" | "ios" | "unknown";
    ttlMinutes: number;
  },
): { artifacts: DeviceOnboardingArtifactRecord[]; artifact?: DeviceOnboardingArtifactRecord; errorStatus?: number; errorTitle?: string; errorDetail?: string } {
  const now = new Date().toISOString();
  const expiresAtUtc = new Date(Date.now() + payload.ttlMinutes * 60_000).toISOString();
  const activationCodeId = crypto.randomUUID();
  const artifact: DeviceOnboardingArtifactRecord = {
    activationCodeId,
    tenantId: payload.tenantId,
    applicationClientId: payload.applicationClientId,
    externalUserId: payload.externalUserId,
    platform: payload.platform,
    status: "pending",
    expiresAtUtc,
    consumedAtUtc: null,
    revokedAtUtc: null,
    createdAtUtc: now,
    activationPayload: `dac_${activationCodeId}.fixture-secret`,
  };

  return {
    artifacts: [artifact, ...artifacts],
    artifact,
  };
}

export function revokeDeviceOnboardingArtifact(
  artifacts: DeviceOnboardingArtifactRecord[],
  tenantId: string,
  activationCodeId: string,
): { artifacts: DeviceOnboardingArtifactRecord[]; artifact?: Omit<DeviceOnboardingArtifactRecord, "activationPayload">; errorStatus?: number; errorTitle?: string; errorDetail?: string } {
  const index = artifacts.findIndex((artifact) => artifact.tenantId === tenantId && artifact.activationCodeId === activationCodeId);
  if (index < 0) {
    return {
      artifacts,
      errorStatus: 404,
      errorTitle: "Device onboarding artifact was not found.",
      errorDetail: "Artifact does not belong to the requested tenant scope.",
    };
  }

  const current = artifacts[index]!;
  if (current.status !== "pending") {
    return {
      artifacts,
      errorStatus: 409,
      errorTitle: "Device onboarding artifact cannot be revoked.",
      errorDetail: "Only pending artifacts can be revoked.",
    };
  }

  const revoked: DeviceOnboardingArtifactRecord = {
    ...current,
    status: "revoked",
    revokedAtUtc: new Date().toISOString(),
  };
  const nextArtifacts = [...artifacts];
  nextArtifacts[index] = revoked;

  return {
    artifacts: nextArtifacts,
    artifact: toDeviceOnboardingArtifactResponse(revoked),
  };
}

export function listIntegrationClients(
  clients: IntegrationClientRecord[],
  tenantId: string,
): IntegrationClientRecord[] {
  return clients.filter((client) => client.tenantId === tenantId);
}

export function getTenantDirectory(
  tenants: TenantDirectoryTenantRecord[],
  applications: TenantDirectoryApplicationRecord[],
  clients: IntegrationClientRecord[],
  tenantId: string,
) {
  const tenant = tenants.find((item) => item.tenantId === tenantId);
  if (!tenant) {
    return null;
  }

  return {
    tenant: refreshTenantCounts(tenant, applications, clients),
    applications: applications
      .filter((application) => application.tenantId === tenantId)
      .map((application) => refreshApplicationCounts(application, clients)),
    integrationClients: clients.filter((client) => client.tenantId === tenantId),
  };
}

export function listTenants(
  tenants: TenantDirectoryTenantRecord[],
  applications: TenantDirectoryApplicationRecord[],
  clients: IntegrationClientRecord[],
): TenantDirectoryTenantRecord[] {
  return tenants.map((tenant) => refreshTenantCounts(tenant, applications, clients));
}

export function createTenant(
  tenants: TenantDirectoryTenantRecord[],
  payload: {
    tenantId?: string;
    displayName: string;
    slug?: string;
    status?: TenantDirectoryStatus;
  },
): { tenants: TenantDirectoryTenantRecord[]; tenant?: TenantDirectoryTenantRecord; errorStatus?: number; errorTitle?: string; errorDetail?: string } {
  const displayName = payload.displayName.trim();
  if (!displayName) {
    return {
      tenants,
      errorStatus: 400,
      errorTitle: "Invalid tenant creation request.",
      errorDetail: "Tenant display name is required.",
    };
  }

  if (tenants.some((tenant) => tenant.displayName.toUpperCase() === displayName.toUpperCase())) {
    return {
      tenants,
      errorStatus: 409,
      errorTitle: "Tenant cannot be created.",
      errorDetail: "Tenant already exists.",
    };
  }

  const now = new Date().toISOString();
  const tenant: TenantDirectoryTenantRecord = {
    tenantId: payload.tenantId || crypto.randomUUID(),
    displayName,
    slug: payload.slug || slugify(displayName),
    status: payload.status || "active",
    applicationCount: 0,
    integrationClientCount: 0,
    createdUtc: now,
    updatedUtc: now,
  };

  return {
    tenants: [tenant, ...tenants],
    tenant,
  };
}

export function quickCreateTenant(
  tenants: TenantDirectoryTenantRecord[],
  applications: TenantDirectoryApplicationRecord[],
  clients: IntegrationClientRecord[],
  payload: {
    tenantDisplayName: string;
    applicationDisplayName: string;
    integrationClientDisplayName: string;
    allowedScopes: string[];
  },
): {
  tenants: TenantDirectoryTenantRecord[];
  applications: TenantDirectoryApplicationRecord[];
  clients: IntegrationClientRecord[];
  directory?: ReturnType<typeof getTenantDirectory>;
  client?: IntegrationClientRecord;
  clientSecret?: string;
  errorStatus?: number;
  errorTitle?: string;
  errorDetail?: string;
} {
  const tenantDisplayName = payload.tenantDisplayName.trim();
  const applicationDisplayName = payload.applicationDisplayName.trim();
  const clientDisplayName = payload.integrationClientDisplayName.trim();
  if (!tenantDisplayName || !applicationDisplayName || !clientDisplayName) {
    return {
      tenants,
      applications,
      clients,
      errorStatus: 400,
      errorTitle: "Invalid tenant quick-create request.",
      errorDetail: "Display names are required.",
    };
  }

  if (tenants.some((tenant) => tenant.displayName.toUpperCase() === tenantDisplayName.toUpperCase())) {
    return {
      tenants,
      applications,
      clients,
      errorStatus: 409,
      errorTitle: "Tenant cannot be quick-created.",
      errorDetail: "Tenant already exists.",
    };
  }

  const now = new Date().toISOString();
  const tenantId = crypto.randomUUID();
  const applicationClientId = crypto.randomUUID();
  const clientId = slugify(`${tenantDisplayName}-${applicationDisplayName}-${clientDisplayName}`);
  const tenant: TenantDirectoryTenantRecord = {
    tenantId,
    displayName: tenantDisplayName,
    slug: slugify(tenantDisplayName),
    status: "active",
    applicationCount: 1,
    integrationClientCount: 1,
    createdUtc: now,
    updatedUtc: now,
  };
  const application: TenantDirectoryApplicationRecord = {
    applicationClientId,
    tenantId,
    displayName: applicationDisplayName,
    slug: slugify(applicationDisplayName),
    status: "active",
    integrationClientCount: 1,
    createdUtc: now,
    updatedUtc: now,
  };
  const client: IntegrationClientRecord = {
    clientId,
    tenantId,
    applicationClientId,
    status: "active",
    allowedScopes: [...payload.allowedScopes].sort(),
    createdUtc: now,
    updatedUtc: now,
    lastSecretRotatedUtc: now,
    lastAuthStateChangedUtc: now,
  };
  const nextTenants = [tenant, ...tenants];
  const nextApplications = [application, ...applications];
  const nextClients = [client, ...clients];

  return {
    tenants: nextTenants,
    applications: nextApplications,
    clients: nextClients,
    directory: getTenantDirectory(nextTenants, nextApplications, nextClients, tenantId),
    client,
    clientSecret: `fixture-tenant-secret-${clientId}`,
  };
}

export function createIntegrationClient(
  clients: IntegrationClientRecord[],
  payload: {
    clientId: string;
    tenantId: string;
    applicationClientId: string;
    allowedScopes: string[];
  },
): { clients: IntegrationClientRecord[]; client?: IntegrationClientRecord; clientSecret?: string; errorStatus?: number; errorTitle?: string; errorDetail?: string } {
  if (clients.some((client) => client.clientId === payload.clientId)) {
    return {
      clients,
      errorStatus: 409,
      errorTitle: "Integration client cannot be created.",
      errorDetail: "Integration client already exists.",
    };
  }

  const now = new Date().toISOString();
  const client: IntegrationClientRecord = {
    clientId: payload.clientId,
    tenantId: payload.tenantId,
    applicationClientId: payload.applicationClientId,
    status: "active",
    allowedScopes: [...payload.allowedScopes].sort(),
    createdUtc: now,
    updatedUtc: null,
    lastSecretRotatedUtc: null,
    lastAuthStateChangedUtc: now,
  };

  return {
    clients: [client, ...clients],
    client,
    clientSecret: `fixture-secret-${payload.clientId}`,
  };
}

export function rotateIntegrationClientSecret(
  clients: IntegrationClientRecord[],
  tenantId: string,
  clientId: string,
): { clients: IntegrationClientRecord[]; client?: IntegrationClientRecord; clientSecret?: string; errorStatus?: number; errorTitle?: string; errorDetail?: string } {
  const index = findIntegrationClientIndex(clients, tenantId, clientId);
  if (index < 0) {
    return integrationClientNotFound(clients);
  }

  const now = new Date().toISOString();
  const client: IntegrationClientRecord = {
    ...clients[index]!,
    updatedUtc: now,
    lastSecretRotatedUtc: now,
    lastAuthStateChangedUtc: now,
  };
  const nextClients = [...clients];
  nextClients[index] = client;

  return {
    clients: nextClients,
    client,
    clientSecret: `fixture-rotated-secret-${clientId}`,
  };
}

export function updateIntegrationClientScopes(
  clients: IntegrationClientRecord[],
  tenantId: string,
  clientId: string,
  allowedScopes: string[],
): { clients: IntegrationClientRecord[]; client?: IntegrationClientRecord; errorStatus?: number; errorTitle?: string; errorDetail?: string } {
  const index = findIntegrationClientIndex(clients, tenantId, clientId);
  if (index < 0) {
    return integrationClientNotFound(clients);
  }

  const now = new Date().toISOString();
  const client: IntegrationClientRecord = {
    ...clients[index]!,
    allowedScopes: [...allowedScopes].sort(),
    updatedUtc: now,
    lastAuthStateChangedUtc: now,
  };
  const nextClients = [...clients];
  nextClients[index] = client;

  return {
    clients: nextClients,
    client,
  };
}

export function setIntegrationClientActiveState(
  clients: IntegrationClientRecord[],
  tenantId: string,
  clientId: string,
  status: "active" | "inactive",
): { clients: IntegrationClientRecord[]; client?: IntegrationClientRecord; errorStatus?: number; errorTitle?: string; errorDetail?: string } {
  const index = findIntegrationClientIndex(clients, tenantId, clientId);
  if (index < 0) {
    return integrationClientNotFound(clients);
  }

  if (clients[index]!.status === status) {
    return {
      clients,
      errorStatus: 409,
      errorTitle: "Integration client state cannot be changed.",
      errorDetail: `Integration client is already ${status}.`,
    };
  }

  const now = new Date().toISOString();
  const client: IntegrationClientRecord = {
    ...clients[index]!,
    status,
    updatedUtc: now,
    lastAuthStateChangedUtc: now,
  };
  const nextClients = [...clients];
  nextClients[index] = client;

  return {
    clients: nextClients,
    client,
  };
}

export function toCurrentResponse(enrollment: EnrollmentRecord) {
  return {
    enrollmentId: enrollment.enrollmentId,
    tenantId: enrollment.tenantId,
    applicationClientId: enrollment.applicationClientId,
    externalUserId: enrollment.externalUserId,
    label: enrollment.label,
    status: enrollment.status,
    hasPendingReplacement: enrollment.hasPendingReplacement,
    confirmedAtUtc: enrollment.confirmedAtUtc,
    revokedAtUtc: enrollment.revokedAtUtc,
  };
}

export function toCommandResponse(enrollment: EnrollmentRecord, artifact?: ArtifactRecord) {
  return {
    enrollmentId: enrollment.enrollmentId,
    status: enrollment.status,
    hasPendingReplacement: enrollment.hasPendingReplacement,
    confirmedAtUtc: enrollment.confirmedAtUtc,
    revokedAtUtc: enrollment.revokedAtUtc,
    secretUri: artifact?.secretUri ?? null,
    qrCodePayload: artifact?.qrCodePayload ?? null,
  };
}

export function toDeviceOnboardingArtifactResponse(
  artifact: DeviceOnboardingArtifactRecord,
): Omit<DeviceOnboardingArtifactRecord, "activationPayload"> {
  const { activationPayload: _activationPayload, ...response } = artifact;
  return response;
}

export function jsonResponse(status: number, body: unknown) {
  return {
    status,
    contentType: "application/json",
    body: JSON.stringify(body),
  };
}

export function problemResponse(status: number, title: string, detail: string) {
  return jsonResponse(status, {
    type: `https://otpauth.dev/problems/${status}`,
    title,
    status,
    detail,
  });
}

function createBase32Secret(seed: string): string {
  return Array.from({ length: 16 }, (_, index) => {
    const charCode = seed.charCodeAt(index % seed.length);
    return base32Alphabet[charCode % base32Alphabet.length];
  }).join("");
}

function findIntegrationClientIndex(
  clients: IntegrationClientRecord[],
  tenantId: string,
  clientId: string,
): number {
  return clients.findIndex((client) => client.tenantId === tenantId && client.clientId === clientId);
}

function integrationClientNotFound<T extends { clients: IntegrationClientRecord[] }>(
  clients: IntegrationClientRecord[],
): T & { errorStatus: number; errorTitle: string; errorDetail: string } {
  return {
    clients,
    errorStatus: 404,
    errorTitle: "Integration client was not found.",
    errorDetail: "Integration client does not belong to the requested tenant scope.",
  } as T & { errorStatus: number; errorTitle: string; errorDetail: string };
}

function refreshTenantCounts(
  tenant: TenantDirectoryTenantRecord,
  applications: TenantDirectoryApplicationRecord[],
  clients: IntegrationClientRecord[],
): TenantDirectoryTenantRecord {
  return {
    ...tenant,
    applicationCount: applications.filter((application) => application.tenantId === tenant.tenantId).length,
    integrationClientCount: clients.filter((client) => client.tenantId === tenant.tenantId).length,
  };
}

function refreshApplicationCounts(
  application: TenantDirectoryApplicationRecord,
  clients: IntegrationClientRecord[],
): TenantDirectoryApplicationRecord {
  return {
    ...application,
    integrationClientCount: clients.filter((client) => (
      client.tenantId === application.tenantId &&
      client.applicationClientId === application.applicationClientId
    )).length,
  };
}

function slugify(value: string): string {
  const slug = value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return slug || "tenant";
}
