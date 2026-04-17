import type { TotpEnrollmentCommandResponse } from "../../../shared/types/admin-contracts";

export interface ProvisioningArtifact {
  enrollmentId: string;
  secretUri: string;
  qrCodePayload: string;
  secret: string;
  issuer: string;
  label: string;
  digits: number;
  period: number;
  algorithm: string;
}

export function parseProvisioningArtifact(response: TotpEnrollmentCommandResponse): ProvisioningArtifact | null {
  const secretUri = response.secretUri?.trim();
  const qrCodePayload = response.qrCodePayload?.trim();
  if (!secretUri || !qrCodePayload) {
    return null;
  }

  const uri = new URL(secretUri);
  const labelPath = decodeURIComponent(uri.pathname.replace(/^\//, ""));
  const [, label = labelPath] = labelPath.split(":");

  return {
    enrollmentId: response.enrollmentId,
    secretUri,
    qrCodePayload,
    secret: uri.searchParams.get("secret") ?? "",
    issuer: uri.searchParams.get("issuer") ?? "",
    label,
    digits: Number.parseInt(uri.searchParams.get("digits") ?? "6", 10),
    period: Number.parseInt(uri.searchParams.get("period") ?? "30", 10),
    algorithm: uri.searchParams.get("algorithm") ?? "SHA1",
  };
}
