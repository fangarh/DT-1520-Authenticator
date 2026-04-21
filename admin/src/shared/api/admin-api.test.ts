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
});
