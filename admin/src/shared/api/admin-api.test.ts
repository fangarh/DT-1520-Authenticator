import { afterEach, describe, expect, it, vi } from "vitest";
import { adminApi } from "./admin-api";

describe("adminApi", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("encodes enrollment id in confirm path", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: "csrf-token" }), { status: 200 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({
          enrollmentId: "enrollment/with space",
          status: "pending",
          hasPendingReplacement: false,
        }), { status: 200 }),
      );

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.confirmEnrollment("enrollment/with space", { code: "123456" });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toBe("/api/v1/admin/enrollments/totp/enrollment%2Fwith%20space/confirm");
  });

  it("encodes application client id in webhook subscription query", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(
        new Response(JSON.stringify([]), { status: 200 }),
      );

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.listWebhookSubscriptions("tenant/with space", "client/with space");

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/webhook-subscriptions?applicationClientId=client%2Fwith%20space",
    );
  });

  it("encodes tenant id and delivery status filters in delivery status query", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(
        new Response(JSON.stringify([]), { status: 200 }),
      );

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.listDeliveryStatuses("tenant/with space", {
      applicationClientId: "client/with space",
      channel: "webhook_event",
      status: "failed",
      limit: 25,
    });

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/delivery-statuses?applicationClientId=client%2Fwith%20space&channel=webhook_event&status=failed&limit=25",
    );
  });

  it("encodes tenant id and external user id in device list path", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(
        new Response(JSON.stringify([]), { status: 200 }),
      );

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.listUserDevices("tenant/with space", "user/with space");

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/users/user%2Fwith%20space/devices",
    );
  });

  it("encodes device revoke path parameters", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: "csrf-token" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ deviceId: "device/with space", status: "revoked" }), { status: 200 }));

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.revokeUserDevice("tenant/with space", "user/with space", "device/with space");

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/users/user%2Fwith%20space/devices/device%2Fwith%20space/revoke",
    );
  });

  it("encodes device onboarding list filters", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200 }));

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.listDeviceOnboardingArtifacts("tenant/with space", {
      externalUserId: "user/with space",
      applicationClientId: "client/with space",
      status: "pending",
      limit: 25,
    });

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/device-onboarding-artifacts?externalUserId=user%2Fwith%20space&applicationClientId=client%2Fwith%20space&status=pending&limit=25",
    );
  });

  it("sends device onboarding create command with csrf token", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: "csrf-token" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        artifact: {
          activationCodeId: "activation-code-id",
          tenantId: "tenant-id",
          applicationClientId: "application-client-id",
          externalUserId: "user-id",
          platform: "android",
          status: "pending",
          expiresAtUtc: "2026-04-27T10:15:00Z",
          createdAtUtc: "2026-04-27T10:00:00Z",
        },
        activationPayload: "one-time-payload",
      }), { status: 201 }));

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.createDeviceOnboardingArtifact({
      tenantId: "tenant-id",
      applicationClientId: "application-client-id",
      externalUserId: "user-id",
      platform: "android",
      ttlMinutes: 15,
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toBe("/api/v1/admin/device-onboarding-artifacts");
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": "csrf-token",
      },
      body: JSON.stringify({
        tenantId: "tenant-id",
        applicationClientId: "application-client-id",
        externalUserId: "user-id",
        platform: "android",
        ttlMinutes: 15,
      }),
    });
  });

  it("encodes device onboarding revoke path and uses csrf token", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: "csrf-token" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ activationCodeId: "artifact/with space", status: "revoked" }), { status: 200 }));

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.revokeDeviceOnboardingArtifact("tenant/with space", "artifact/with space");

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/device-onboarding-artifacts/artifact%2Fwith%20space/revoke",
    );
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: "POST",
      headers: {
        "X-CSRF-TOKEN": "csrf-token",
      },
    });
  });

  it("encodes tenant id in integration client list path", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200 }));

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.listIntegrationClients("tenant/with space");

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/integration-clients",
    );
  });

  it("sends integration client create command with csrf token", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: "csrf-token" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        client: {
          clientId: "project-manager",
          tenantId: "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb",
          applicationClientId: "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4",
          status: "active",
          allowedScopes: ["challenges:read"],
          createdUtc: "2026-04-27T10:00:00Z",
          lastAuthStateChangedUtc: "2026-04-27T10:00:00Z",
        },
        clientSecret: "one-time-secret",
      }), { status: 201 }));

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.createIntegrationClient({
      clientId: "project-manager",
      tenantId: "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb",
      applicationClientId: "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4",
      allowedScopes: ["challenges:read"],
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toBe("/api/v1/admin/integration-clients");
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": "csrf-token",
      },
    });
  });

  it("sends integration client rotate command with csrf token and encoded route", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: "csrf-token" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ client: { clientId: "client/with space" }, clientSecret: "rotated-secret" }), { status: 200 }));

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.rotateIntegrationClientSecret("tenant/with space", "client/with space");

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/integration-clients/client%2Fwith%20space/rotate-secret",
    );
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: "POST",
      headers: {
        "X-CSRF-TOKEN": "csrf-token",
      },
    });
  });

  it("sends integration client scope update with csrf token and encoded route", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: "csrf-token" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ clientId: "client/with space" }), { status: 200 }));

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.updateIntegrationClientScopes("tenant/with space", "client/with space", {
      allowedScopes: ["challenges:read", "devices:write"],
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/integration-clients/client%2Fwith%20space/scopes",
    );
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": "csrf-token",
      },
      body: JSON.stringify({ allowedScopes: ["challenges:read", "devices:write"] }),
    });
  });

  it("sends integration client active state commands with csrf token and encoded route", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: "csrf-token" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ clientId: "client/with space", status: "inactive" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: "csrf-token" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ clientId: "client/with space", status: "active" }), { status: 200 }));

    vi.stubGlobal("fetch", fetchMock);

    await adminApi.deactivateIntegrationClient("tenant/with space", "client/with space");
    await adminApi.reactivateIntegrationClient("tenant/with space", "client/with space");

    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/integration-clients/client%2Fwith%20space/deactivate",
    );
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({ method: "POST" });
    expect(fetchMock.mock.calls[3]?.[0]).toBe(
      "/api/v1/admin/tenants/tenant%2Fwith%20space/integration-clients/client%2Fwith%20space/reactivate",
    );
    expect(fetchMock.mock.calls[3]?.[1]).toMatchObject({ method: "POST" });
  });
});
