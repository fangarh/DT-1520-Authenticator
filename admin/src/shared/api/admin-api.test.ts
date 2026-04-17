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
});
