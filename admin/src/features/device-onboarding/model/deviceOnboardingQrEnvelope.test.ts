import { describe, expect, it } from "vitest";
import {
  createDeviceOnboardingQrEnvelopeValue,
  resolveDeviceOnboardingRuntimeBaseUrl,
  type DeviceOnboardingQrEnvelope,
} from "./deviceOnboardingQrEnvelope";

describe("device onboarding QR envelope", () => {
  it("wraps the one-time activation payload in a v1 runtime envelope", () => {
    const value = createDeviceOnboardingQrEnvelopeValue({
      runtimeBaseUrl: "https://admin.ghostring.ru:18443/",
      activationPayload: "dac_11111111-1111-1111-1111-111111111111.secret",
    });

    expect(JSON.parse(value) as DeviceOnboardingQrEnvelope).toEqual({
      v: 1,
      runtimeBaseUrl: "https://admin.ghostring.ru:18443",
      activationPayload: "dac_11111111-1111-1111-1111-111111111111.secret",
    });
  });

  it("uses current public origin when admin API base URL is same-origin", () => {
    expect(resolveDeviceOnboardingRuntimeBaseUrl({
      configuredApiBaseUrl: "",
      currentOrigin: "https://admin.ghostring.ru:18443",
    })).toBe("https://admin.ghostring.ru:18443");
  });

  it("derives same-origin runtime URL from relative admin API config", () => {
    expect(resolveDeviceOnboardingRuntimeBaseUrl({
      configuredApiBaseUrl: "/admin-api",
      currentOrigin: "https://admin.example.test",
    })).toBe("https://admin.example.test");
  });

  it("uses an absolute configured admin API base URL as the runtime URL source", () => {
    expect(resolveDeviceOnboardingRuntimeBaseUrl({
      configuredApiBaseUrl: "https://runtime.example.test/",
      currentOrigin: "https://admin.example.test",
    })).toBe("https://runtime.example.test");
  });

  it("rejects credential-bearing runtime URLs", () => {
    expect(() => createDeviceOnboardingQrEnvelopeValue({
      runtimeBaseUrl: "https://operator:secret@admin.example.test",
      activationPayload: "dac_11111111-1111-1111-1111-111111111111.secret",
    })).toThrow(/must not include credentials/i);
  });
});
