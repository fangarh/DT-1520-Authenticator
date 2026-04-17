import { describe, expect, it } from "vitest";
import { parseProvisioningArtifact } from "./provisioning-artifact";

describe("parseProvisioningArtifact", () => {
  it("extracts manual provisioning fields from otpauth uri", () => {
    const artifact = parseProvisioningArtifact({
      enrollmentId: "e-1",
      status: "pending",
      hasPendingReplacement: false,
      secretUri: "otpauth://totp/OTPAuth:alice?secret=ABC123&issuer=OTPAuth&digits=6&period=30&algorithm=SHA1",
      qrCodePayload: "otpauth://totp/OTPAuth:alice?secret=ABC123&issuer=OTPAuth&digits=6&period=30&algorithm=SHA1",
    });

    expect(artifact).not.toBeNull();
    expect(artifact?.secret).toBe("ABC123");
    expect(artifact?.label).toBe("alice");
    expect(artifact?.period).toBe(30);
  });

  it("returns null when provisioning artifacts are absent", () => {
    const artifact = parseProvisioningArtifact({
      enrollmentId: "e-2",
      status: "confirmed",
      hasPendingReplacement: false,
    });

    expect(artifact).toBeNull();
  });
});
