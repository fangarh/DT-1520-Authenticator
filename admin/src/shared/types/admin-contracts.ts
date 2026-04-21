export type TotpEnrollmentStatus = "pending" | "confirmed" | "revoked";

export interface AdminSession {
  adminUserId: string;
  username: string;
  permissions: string[];
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
}

export interface TotpEnrollmentCurrent {
  enrollmentId: string;
  tenantId: string;
  applicationClientId: string;
  externalUserId: string;
  label?: string | null;
  status: TotpEnrollmentStatus;
  hasPendingReplacement: boolean;
  confirmedAtUtc?: string | null;
  revokedAtUtc?: string | null;
}

export interface TotpEnrollmentCommandResponse {
  enrollmentId: string;
  status: TotpEnrollmentStatus;
  hasPendingReplacement: boolean;
  confirmedAtUtc?: string | null;
  revokedAtUtc?: string | null;
  secretUri?: string | null;
  qrCodePayload?: string | null;
}

export interface StartEnrollmentRequest {
  tenantId: string;
  applicationClientId?: string;
  externalUserId: string;
  issuer?: string;
  label?: string;
}

export interface ConfirmEnrollmentRequest {
  code: string;
}

export type WebhookSubscriptionStatus = "active" | "inactive";

export type WebhookEventType =
  | "challenge.approved"
  | "challenge.denied"
  | "challenge.expired"
  | "device.activated"
  | "device.revoked"
  | "device.blocked"
  | "factor.revoked";

export interface WebhookSubscriptionView {
  subscriptionId: string;
  tenantId: string;
  applicationClientId: string;
  endpointUrl: string;
  status: WebhookSubscriptionStatus;
  eventTypes: WebhookEventType[];
  createdUtc: string;
  updatedUtc?: string | null;
}

export interface UpsertWebhookSubscriptionRequest {
  tenantId: string;
  applicationClientId?: string;
  endpointUrl: string;
  eventTypes: WebhookEventType[];
  isActive: boolean;
}

export type AdminDeliveryChannel = "challenge_callback" | "webhook_event";

export type AdminDeliveryStatus = "queued" | "delivered" | "failed";

export interface AdminDeliveryStatusView {
  deliveryId: string;
  tenantId: string;
  applicationClientId: string;
  channel: AdminDeliveryChannel;
  status: AdminDeliveryStatus;
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

export interface AdminDeliveryStatusListFilters {
  applicationClientId?: string;
  channel?: AdminDeliveryChannel;
  status?: AdminDeliveryStatus;
  limit?: number;
}

export type AdminUserDevicePlatform = "android" | "ios" | "unknown";

export type AdminUserDeviceStatus = "active" | "revoked" | "blocked";

export interface AdminUserDeviceView {
  deviceId: string;
  platform: AdminUserDevicePlatform;
  status: AdminUserDeviceStatus;
  isPushCapable: boolean;
  activatedAtUtc?: string | null;
  lastSeenAtUtc?: string | null;
  revokedAtUtc?: string | null;
  blockedAtUtc?: string | null;
}
