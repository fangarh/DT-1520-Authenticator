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
