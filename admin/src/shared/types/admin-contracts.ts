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

export type AdminDeviceOnboardingStatus = "pending" | "consumed" | "expired" | "revoked";

export type AdminDeviceOnboardingPlatform = "android" | "ios" | "unknown";

export interface AdminDeviceOnboardingArtifactView {
  activationCodeId: string;
  tenantId: string;
  applicationClientId: string;
  externalUserId: string;
  platform: AdminDeviceOnboardingPlatform;
  status: AdminDeviceOnboardingStatus;
  expiresAtUtc: string;
  consumedAtUtc?: string | null;
  revokedAtUtc?: string | null;
  createdAtUtc: string;
}

export interface AdminDeviceOnboardingListFilters {
  externalUserId?: string;
  applicationClientId?: string;
  status?: AdminDeviceOnboardingStatus;
  limit?: number;
}

export interface AdminCreateDeviceOnboardingArtifactRequest {
  tenantId: string;
  applicationClientId: string;
  externalUserId: string;
  platform: AdminDeviceOnboardingPlatform;
  ttlMinutes: number;
}

export interface AdminCreateDeviceOnboardingArtifactResponse {
  artifact: AdminDeviceOnboardingArtifactView;
  activationPayload: string;
}

export type AdminIntegrationClientStatus = "active" | "inactive";

export type AdminIntegrationClientScope =
  | "challenges:read"
  | "challenges:write"
  | "enrollments:write"
  | "devices:write";

export interface AdminIntegrationClientView {
  clientId: string;
  tenantId: string;
  applicationClientId: string;
  status: AdminIntegrationClientStatus;
  allowedScopes: AdminIntegrationClientScope[];
  createdUtc: string;
  updatedUtc?: string | null;
  lastSecretRotatedUtc?: string | null;
  lastAuthStateChangedUtc: string;
}

export interface AdminCreateIntegrationClientRequest {
  clientId: string;
  tenantId: string;
  applicationClientId: string;
  allowedScopes: AdminIntegrationClientScope[];
}

export interface AdminUpdateIntegrationClientScopesRequest {
  allowedScopes: AdminIntegrationClientScope[];
}

export interface AdminIntegrationClientSecretResponse {
  client: AdminIntegrationClientView;
  clientSecret: string;
}

export type AdminTenantDirectoryStatus = "active" | "disabled" | "archived" | "test";

export interface AdminTenantDirectoryTenantView {
  tenantId: string;
  displayName: string;
  slug?: string | null;
  status: AdminTenantDirectoryStatus;
  applicationCount: number;
  integrationClientCount: number;
  createdUtc: string;
  updatedUtc?: string | null;
}

export interface AdminTenantDirectoryApplicationView {
  applicationClientId: string;
  tenantId: string;
  displayName: string;
  slug?: string | null;
  status: AdminTenantDirectoryStatus;
  integrationClientCount: number;
  createdUtc: string;
  updatedUtc?: string | null;
}

export interface AdminTenantDirectoryDetailView {
  tenant: AdminTenantDirectoryTenantView;
  applications: AdminTenantDirectoryApplicationView[];
  integrationClients: AdminIntegrationClientView[];
}

export interface AdminCreateTenantRequest {
  tenantId?: string;
  displayName: string;
  slug?: string;
  status?: AdminTenantDirectoryStatus;
}

export interface AdminQuickCreateTenantRequest {
  tenantDisplayName: string;
  applicationDisplayName: string;
  integrationClientDisplayName: string;
  allowedScopes: AdminIntegrationClientScope[];
}

export interface AdminQuickCreateTenantResponse {
  directory: AdminTenantDirectoryDetailView;
  client: AdminIntegrationClientView;
  clientSecret: string;
}

export interface AdminRuntimeConfigurationView {
  callbackUrlPolicy: {
    mode: string;
    allowInsecureHttp: boolean;
  };
}
