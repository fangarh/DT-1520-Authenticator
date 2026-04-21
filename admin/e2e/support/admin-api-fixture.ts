import { expect, type Page } from "@playwright/test";
import { matchesTotpCode } from "../../test-support/totp-code";
import {
  createArtifact,
  createSeedDeliveryStatuses,
  createSeedUserDevices,
  jsonResponse,
  listDeliveryStatuses,
  listUserDevices,
  problemResponse,
  revokeUserDevice,
  toCommandResponse,
  toCurrentResponse,
  type ArtifactRecord,
  type EnrollmentRecord,
  type UserDeviceRecord,
  type WebhookSubscriptionRecord,
} from "./admin-api-fixture-models";

export async function installAdminApiFixture(page: Page) {
  const csrfToken = "fixture-csrf-token";
  const defaultApplicationClientId = "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4";
  const session = {
    adminUserId: "f82103dc-2d30-46b7-94d5-7e9bf1dc7d4f",
    username: "operator",
    permissions: ["enrollments.read", "enrollments.write", "devices.read", "devices.write", "webhooks.read", "webhooks.write"],
  };
  let isAuthenticated = false;
  let enrollment: EnrollmentRecord | null = null;
  let activeArtifact: ArtifactRecord | null = null;
  let replacementArtifact: ArtifactRecord | null = null;
  let webhookSubscriptions: WebhookSubscriptionRecord[] = [];
  const deliveryStatuses = createSeedDeliveryStatuses("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb", defaultApplicationClientId);
  let userDevices: UserDeviceRecord[] = createSeedUserDevices("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb", "user-device");

  await page.route("**/api/v1/admin/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());

    if (url.pathname === "/api/v1/admin/auth/csrf-token" && request.method() === "GET") {
      await route.fulfill(jsonResponse(200, { requestToken: csrfToken }));
      return;
    }

    if (request.method() === "POST") {
      expect(request.headers()["x-csrf-token"]).toBe(csrfToken);
    }

    if (url.pathname === "/api/v1/admin/auth/login" && request.method() === "POST") {
      const payload = await request.postDataJSON();
      if (payload.username !== "operator" || payload.password !== "super-secret") {
        await route.fulfill(problemResponse(401, "Admin authentication failed.", "Invalid username or password."));
        return;
      }

      isAuthenticated = true;
      await route.fulfill(jsonResponse(200, session));
      return;
    }

    if (url.pathname === "/api/v1/admin/auth/logout" && request.method() === "POST") {
      if (!isAuthenticated) {
        await route.fulfill(problemResponse(401, "Authentication failed.", "Session is not active."));
        return;
      }

      isAuthenticated = false;
      await route.fulfill({ status: 204 });
      return;
    }

    if (url.pathname === "/api/v1/admin/auth/session" && request.method() === "GET") {
      await route.fulfill(
        isAuthenticated
          ? jsonResponse(200, session)
          : problemResponse(401, "Authentication failed.", "Admin session is not active."),
      );
      return;
    }

    if (!isAuthenticated) {
      await route.fulfill(problemResponse(401, "Authentication failed.", "Admin session is not active."));
      return;
    }

    if (url.pathname.endsWith("/enrollments/totp/current") && request.method() === "GET") {
      await route.fulfill(
        enrollment
          ? jsonResponse(200, toCurrentResponse(enrollment))
          : problemResponse(404, "Enrollment not found.", "Current TOTP enrollment was not found."),
      );
      return;
    }

    if (url.pathname.includes("/webhook-subscriptions") && request.method() === "GET") {
      const tenantId = url.pathname.split("/")[5];
      const applicationClientId = url.searchParams.get("applicationClientId");
      const subscriptions = webhookSubscriptions.filter((subscription) => (
        subscription.tenantId === tenantId &&
        (!applicationClientId || subscription.applicationClientId === applicationClientId)
      ));

      await route.fulfill(jsonResponse(200, subscriptions));
      return;
    }

    if (url.pathname.endsWith("/delivery-statuses") && request.method() === "GET") {
      const tenantId = url.pathname.split("/")[5];
      await route.fulfill(jsonResponse(200, listDeliveryStatuses(deliveryStatuses, tenantId, url.searchParams)));
      return;
    }

    if (url.pathname.endsWith("/devices") && request.method() === "GET") {
      const tenantId = url.pathname.split("/")[5];
      const externalUserId = decodeURIComponent(url.pathname.split("/")[7] ?? "");
      await route.fulfill(jsonResponse(200, listUserDevices(userDevices, tenantId, externalUserId)));
      return;
    }

    if (url.pathname === "/api/v1/admin/enrollments/totp" && request.method() === "POST") {
      const payload = await request.postDataJSON();
      const artifact = createArtifact(payload.issuer ?? "OTPAuth", payload.label || payload.externalUserId, Date.now().toString(32));
      enrollment = {
        enrollmentId: crypto.randomUUID(),
        tenantId: payload.tenantId,
        applicationClientId: payload.applicationClientId,
        externalUserId: payload.externalUserId,
        label: payload.label || payload.externalUserId,
        status: "pending",
        hasPendingReplacement: false,
        confirmedAtUtc: null,
        revokedAtUtc: null,
      };
      activeArtifact = artifact;
      replacementArtifact = null;
      await route.fulfill(jsonResponse(200, toCommandResponse(enrollment, artifact)));
      return;
    }

    if (url.pathname.endsWith("/replace") && request.method() === "POST") {
      if (!enrollment || enrollment.status !== "confirmed") {
        await route.fulfill(problemResponse(409, "Replacement is not allowed.", "Enrollment must be confirmed before replace."));
        return;
      }

      replacementArtifact = createArtifact("OTPAuth Playwright", enrollment.label, `${Date.now().toString(32)}-replacement`);
      enrollment = {
        ...enrollment,
        hasPendingReplacement: true,
      };
      await route.fulfill(jsonResponse(200, toCommandResponse(enrollment, replacementArtifact)));
      return;
    }

    if (url.pathname.endsWith("/confirm") && request.method() === "POST") {
      const payload = await request.postDataJSON();
      const artifact = enrollment?.hasPendingReplacement ? replacementArtifact : activeArtifact;
      if (!enrollment || !artifact) {
        await route.fulfill(problemResponse(409, "Confirmation is not allowed.", "Enrollment is not awaiting confirmation."));
        return;
      }

      const isValidCode = matchesTotpCode({
        secret: artifact.secret,
        code: payload.code,
      });
      if (!isValidCode) {
        await route.fulfill(problemResponse(422, "Invalid TOTP code.", "Authenticator code did not match the current secret."));
        return;
      }

      enrollment = {
        ...enrollment,
        status: "confirmed",
        hasPendingReplacement: false,
        confirmedAtUtc: new Date().toISOString(),
      };
      activeArtifact = artifact;
      replacementArtifact = null;
      await route.fulfill(jsonResponse(200, toCommandResponse(enrollment)));
      return;
    }

    if (url.pathname.endsWith("/revoke") && url.pathname.includes("/devices/") && request.method() === "POST") {
      const parts = url.pathname.split("/");
      const tenantId = parts[5] ?? "";
      const externalUserId = decodeURIComponent(parts[7] ?? "");
      const deviceId = decodeURIComponent(parts[9] ?? "");
      const result = revokeUserDevice(userDevices, tenantId, externalUserId, deviceId);

      if (result.errorStatus) {
        await route.fulfill(problemResponse(result.errorStatus, result.errorTitle ?? "Request failed.", result.errorDetail ?? "Request failed."));
        return;
      }

      userDevices = result.devices;
      await route.fulfill(jsonResponse(200, result.revokedDevice));
      return;
    }

    if (url.pathname.endsWith("/revoke") && request.method() === "POST") {
      if (!enrollment) {
        await route.fulfill(problemResponse(404, "Enrollment not found.", "Current TOTP enrollment was not found."));
        return;
      }

      enrollment = {
        ...enrollment,
        status: "revoked",
        hasPendingReplacement: false,
        revokedAtUtc: new Date().toISOString(),
      };
      activeArtifact = null;
      replacementArtifact = null;
      await route.fulfill(jsonResponse(200, toCommandResponse(enrollment)));
      return;
    }

    if (url.pathname === "/api/v1/admin/webhook-subscriptions" && request.method() === "POST") {
      const payload = await request.postDataJSON();
      const existingIndex = webhookSubscriptions.findIndex((subscription) => (
        subscription.tenantId === payload.tenantId &&
        subscription.applicationClientId === (payload.applicationClientId || defaultApplicationClientId) &&
        subscription.endpointUrl === payload.endpointUrl
      ));
      const now = new Date().toISOString();
      const subscription: WebhookSubscriptionRecord = {
        subscriptionId: existingIndex >= 0
          ? webhookSubscriptions[existingIndex]!.subscriptionId
          : crypto.randomUUID(),
        tenantId: payload.tenantId,
        applicationClientId: payload.applicationClientId || defaultApplicationClientId,
        endpointUrl: payload.endpointUrl,
        status: payload.isActive === false ? "inactive" : "active",
        eventTypes: [...payload.eventTypes].sort(),
        createdUtc: existingIndex >= 0
          ? webhookSubscriptions[existingIndex]!.createdUtc
          : now,
        updatedUtc: now,
      };

      if (existingIndex >= 0) {
        webhookSubscriptions[existingIndex] = subscription;
      } else {
        webhookSubscriptions = [...webhookSubscriptions, subscription];
      }

      await route.fulfill(jsonResponse(200, subscription));
      return;
    }

    await route.fulfill(problemResponse(404, "Not found.", `No fixture route for ${request.method()} ${url.pathname}.`));
  });
}
