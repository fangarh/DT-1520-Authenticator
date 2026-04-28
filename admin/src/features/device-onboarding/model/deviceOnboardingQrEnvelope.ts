import { runtimeConfig } from "../../../shared/config/runtime";

export interface DeviceOnboardingQrEnvelope {
  v: 1;
  runtimeBaseUrl: string;
  activationPayload: string;
}

interface RuntimeBaseUrlOptions {
  configuredApiBaseUrl?: string;
  currentOrigin?: string;
}

interface QrEnvelopeOptions {
  activationPayload: string;
  runtimeBaseUrl: string;
}

export function resolveDeviceOnboardingRuntimeBaseUrl(options: RuntimeBaseUrlOptions = {}): string {
  const configuredApiBaseUrl = options.configuredApiBaseUrl ?? runtimeConfig.apiBaseUrl;
  const currentOrigin = options.currentOrigin ?? getCurrentOrigin();
  const configured = configuredApiBaseUrl.trim();

  if (!configured) {
    return normalizeRuntimeBaseUrl(currentOrigin);
  }

  if (hasUrlScheme(configured)) {
    return normalizeRuntimeBaseUrl(configured);
  }

  const resolved = new URL(configured, normalizeRuntimeBaseUrl(currentOrigin));
  return normalizeRuntimeBaseUrl(resolved.origin);
}

export function createDeviceOnboardingQrEnvelopeValue(options: QrEnvelopeOptions): string {
  const activationPayload = options.activationPayload.trim();
  if (!activationPayload) {
    throw new Error("Device onboarding activation payload is empty.");
  }

  const envelope: DeviceOnboardingQrEnvelope = {
    v: 1,
    runtimeBaseUrl: normalizeRuntimeBaseUrl(options.runtimeBaseUrl),
    activationPayload,
  };

  return JSON.stringify(envelope);
}

function normalizeRuntimeBaseUrl(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    throw new Error("Device onboarding runtime base URL is empty.");
  }

  const url = new URL(trimmed);
  if (url.protocol !== "https:" && url.protocol !== "http:") {
    throw new Error("Device onboarding runtime base URL must use http or https.");
  }

  if (!url.hostname) {
    throw new Error("Device onboarding runtime base URL must include a host.");
  }

  if (url.username || url.password) {
    throw new Error("Device onboarding runtime base URL must not include credentials.");
  }

  url.search = "";
  url.hash = "";

  return url.toString().replace(/\/$/, "");
}

function getCurrentOrigin(): string {
  if (typeof window === "undefined") {
    return "";
  }

  return window.location.origin;
}

function hasUrlScheme(value: string): boolean {
  return /^[a-z][a-z\d+.-]*:/i.test(value);
}
