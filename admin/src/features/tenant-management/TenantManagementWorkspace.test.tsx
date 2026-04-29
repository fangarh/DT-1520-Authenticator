import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type {
  AdminIntegrationClientView,
  AdminSession,
  AdminTenantDirectoryDetailView,
} from "../../shared/types/admin-contracts";
import { adminApi } from "../../shared/api/admin-api";
import { TenantManagementWorkspace } from "./TenantManagementWorkspace";

vi.mock("../../shared/api/admin-api", () => ({
  adminApi: {
    listIntegrationClients: vi.fn(),
    createIntegrationClient: vi.fn(),
    rotateIntegrationClientSecret: vi.fn(),
    updateIntegrationClientScopes: vi.fn(),
    deactivateIntegrationClient: vi.fn(),
    reactivateIntegrationClient: vi.fn(),
    listUserDevices: vi.fn(),
    createDeviceOnboardingArtifact: vi.fn(),
    createCombinedOnboardingPackage: vi.fn(),
    listDeviceOnboardingArtifacts: vi.fn(),
    revokeUserDevice: vi.fn(),
    getRuntimeConfiguration: vi.fn(),
    listDeliveryStatuses: vi.fn(),
  },
}));

const session: AdminSession = {
  adminUserId: "admin-1",
  username: "operator",
  permissions: [
    "integration-clients.read",
    "integration-clients.write",
    "devices.read",
    "devices.write",
    "enrollments.write",
    "webhooks.read",
  ],
};

const client: AdminIntegrationClientView = {
  clientId: "client-1",
  tenantId: "tenant-1",
  applicationClientId: "app-1",
  status: "active",
  allowedScopes: ["challenges:read", "challenges:write"],
  createdUtc: "2026-04-28T10:00:00Z",
  updatedUtc: null,
  lastSecretRotatedUtc: null,
  lastAuthStateChangedUtc: "2026-04-28T10:00:00Z",
};

const directory: AdminTenantDirectoryDetailView = {
  tenant: {
    tenantId: "tenant-1",
    displayName: "Managed Tenant",
    slug: "managed-tenant",
    status: "active",
    applicationCount: 1,
    integrationClientCount: 1,
    createdUtc: "2026-04-28T09:00:00Z",
    updatedUtc: null,
  },
  applications: [
    {
      applicationClientId: "app-1",
      tenantId: "tenant-1",
      displayName: "Managed App",
      slug: "managed-app",
      status: "active",
      integrationClientCount: 1,
      createdUtc: "2026-04-28T09:00:00Z",
      updatedUtc: null,
    },
  ],
  integrationClients: [client],
};

describe("TenantManagementWorkspace", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(adminApi.createIntegrationClient).mockResolvedValue({
      client: { ...client, clientId: "tenant-created-client" },
      clientSecret: "created-client-secret",
    });
    vi.mocked(adminApi.rotateIntegrationClientSecret).mockResolvedValue({
      client: { ...client, lastSecretRotatedUtc: "2026-04-28T10:10:00Z" },
      clientSecret: "rotated-client-secret",
    });
    vi.mocked(adminApi.listUserDevices).mockResolvedValue([
      {
        deviceId: "device-1",
        platform: "android",
        status: "active",
        isPushCapable: true,
        activatedAtUtc: "2026-04-28T10:00:00Z",
        lastSeenAtUtc: "2026-04-28T10:05:00Z",
        revokedAtUtc: null,
        blockedAtUtc: null,
      },
    ]);
    vi.mocked(adminApi.createCombinedOnboardingPackage).mockResolvedValue({
      deviceArtifact: {
        activationCodeId: "activation-1",
        tenantId: "tenant-1",
        applicationClientId: "app-1",
        externalUserId: "user-1",
        platform: "android",
        status: "pending",
        expiresAtUtc: "2026-04-28T10:30:00Z",
        consumedAtUtc: null,
        revokedAtUtc: null,
        createdAtUtc: "2026-04-28T10:00:00Z",
      },
      activationPayload: "dac_activation-1.secret",
      totpEnrollment: {
        enrollmentId: "totp-enrollment-1",
        status: "pending",
        hasPendingReplacement: false,
        confirmedAtUtc: null,
        revokedAtUtc: null,
        secretUri: "otpauth://totp/OTPAuth:user-1?secret=SECRET&issuer=OTPAuth",
        qrCodePayload: "otpauth://totp/OTPAuth:user-1?secret=SECRET&issuer=OTPAuth",
      },
    });
    vi.mocked(adminApi.getRuntimeConfiguration).mockResolvedValue({
      callbackUrlPolicy: {
        mode: "PrivateNetwork",
        allowInsecureHttp: false,
      },
    });
    vi.mocked(adminApi.listDeliveryStatuses).mockResolvedValue([
      {
        deliveryId: "delivery-approved",
        tenantId: "tenant-1",
        applicationClientId: "app-1",
        channel: "challenge_callback",
        status: "delivered",
        eventType: "challenge.approved",
        deliveryDestination: "https://reference.example/callbacks/dt1520",
        subjectType: "challenge",
        subjectId: "challenge-1",
        publicationId: null,
        attemptCount: 1,
        occurredAtUtc: "2026-04-28T10:20:00Z",
        createdAtUtc: "2026-04-28T10:20:01Z",
        nextAttemptAtUtc: "2026-04-28T10:20:01Z",
        lastAttemptAtUtc: "2026-04-28T10:20:02Z",
        deliveredAtUtc: "2026-04-28T10:20:02Z",
        lastErrorCode: null,
        isRetryScheduled: false,
      },
      {
        deliveryId: "delivery-failed",
        tenantId: "tenant-1",
        applicationClientId: "app-1",
        channel: "webhook_event",
        status: "failed",
        eventType: "challenge.denied",
        deliveryDestination: "https://reference.example/webhooks",
        subjectType: "challenge",
        subjectId: "challenge-2",
        publicationId: "publication-1",
        attemptCount: 3,
        occurredAtUtc: "2026-04-28T10:25:00Z",
        createdAtUtc: "2026-04-28T10:25:01Z",
        nextAttemptAtUtc: "2026-04-28T10:30:01Z",
        lastAttemptAtUtc: "2026-04-28T10:26:00Z",
        deliveredAtUtc: null,
        lastErrorCode: "delivery_failed",
        isRetryScheduled: true,
      },
    ]);
    vi.mocked(adminApi.listDeviceOnboardingArtifacts).mockResolvedValue([
      {
        activationCodeId: "activation-report-1",
        tenantId: "tenant-1",
        applicationClientId: "app-1",
        externalUserId: "user-1",
        platform: "android",
        status: "consumed",
        expiresAtUtc: "2026-04-28T10:30:00Z",
        consumedAtUtc: "2026-04-28T10:22:00Z",
        revokedAtUtc: null,
        createdAtUtc: "2026-04-28T10:00:00Z",
      },
    ]);
  });

  it("routes API client create and rotate through selected tenant context", async () => {
    const user = userEvent.setup();
    render(<TenantManagementWorkspace session={session} directory={directory} />);

    await user.click(screen.getByRole("tab", { name: "API clients" }));
    await user.type(screen.getByLabelText("Client ID"), "tenant-created-client");
    await user.click(screen.getByRole("button", { name: "Create client in selected tenant" }));

    await waitFor(() => expect(adminApi.createIntegrationClient).toHaveBeenCalledWith({
      tenantId: "tenant-1",
      applicationClientId: "app-1",
      clientId: "tenant-created-client",
      allowedScopes: ["challenges:read", "challenges:write"],
    }));
    expect(await screen.findByText("created-client-secret")).toBeTruthy();

    await user.click(screen.getByLabelText("Rotate client secret and invalidate the old secret."));
    await user.click(screen.getByRole("button", { name: "Rotate secret" }));

    await waitFor(() => expect(adminApi.rotateIntegrationClientSecret).toHaveBeenCalledWith("tenant-1", "tenant-created-client"));
    expect(await screen.findByText("rotated-client-secret")).toBeTruthy();
  });

  it("routes selected-user device and QR actions through tenant/application context", async () => {
    const user = userEvent.setup();
    render(<TenantManagementWorkspace session={session} directory={directory} />);

    await user.click(screen.getByRole("tab", { name: "Users & devices" }));
    await user.type(screen.getByLabelText("External User ID"), "user-1");
    await user.click(screen.getByRole("button", { name: "Load user devices" }));

    await waitFor(() => expect(adminApi.listUserDevices).toHaveBeenCalledWith("tenant-1", "user-1"));
    expect(await screen.findByText("device-1")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Issue combined QR for selected user" }));

    await waitFor(() => expect(adminApi.createCombinedOnboardingPackage).toHaveBeenCalledWith({
      tenantId: "tenant-1",
      applicationClientId: "app-1",
      externalUserId: "user-1",
      platform: "android",
      ttlMinutes: 15,
      label: "user-1",
    }));
    expect(await screen.findByLabelText("One-time combined onboarding QR")).toBeTruthy();
    expect(await screen.findByText("TOTP enrollment: totp-enrollment-1")).toBeTruthy();
    expect(screen.queryByText(/SECRET/)).toBeNull();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it("loads runtime policy and report data without secret-bearing fields", async () => {
    const user = userEvent.setup();
    render(<TenantManagementWorkspace session={session} directory={directory} />);

    await user.click(screen.getByRole("tab", { name: "Runtime" }));
    await user.click(screen.getByRole("button", { name: "Load runtime configuration" }));

    await waitFor(() => expect(adminApi.getRuntimeConfiguration).toHaveBeenCalledOnce());
    expect(await screen.findByText("PrivateNetwork")).toBeTruthy();

    await user.click(screen.getByRole("tab", { name: "Reports" }));
    await user.click(screen.getByRole("button", { name: "Refresh report snapshot" }));

    await waitFor(() => expect(adminApi.listDeliveryStatuses).toHaveBeenCalledWith("tenant-1", {
      applicationClientId: "app-1",
      limit: 25,
    }));
    expect(adminApi.listDeviceOnboardingArtifacts).toHaveBeenCalledWith("tenant-1", {
      applicationClientId: "app-1",
      externalUserId: undefined,
      limit: 25,
    });
    expect(await screen.findByText("challenge.approved / delivered")).toBeTruthy();
    expect(await screen.findByText("consumed / android")).toBeTruthy();
    expect(screen.getByText("Last approved callback")).toBeTruthy();
  });
});
