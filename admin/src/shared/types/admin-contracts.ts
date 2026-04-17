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
