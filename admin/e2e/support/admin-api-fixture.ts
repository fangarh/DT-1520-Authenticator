import { expect, type Page } from "@playwright/test";
import { matchesTotpCode } from "../../test-support/totp-code";

const base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

type EnrollmentStatus = "pending" | "confirmed" | "revoked";

interface ArtifactRecord {
  secret: string;
  secretUri: string;
  qrCodePayload: string;
}

interface EnrollmentRecord {
  enrollmentId: string;
  tenantId: string;
  applicationClientId: string;
  externalUserId: string;
  label: string;
  status: EnrollmentStatus;
  hasPendingReplacement: boolean;
  confirmedAtUtc: string | null;
  revokedAtUtc: string | null;
}

export async function installAdminApiFixture(page: Page) {
  const csrfToken = "fixture-csrf-token";
  const session = {
    adminUserId: "f82103dc-2d30-46b7-94d5-7e9bf1dc7d4f",
    username: "operator",
    permissions: ["enrollments.read", "enrollments.write"],
  };
  let isAuthenticated = false;
  let enrollment: EnrollmentRecord | null = null;
  let activeArtifact: ArtifactRecord | null = null;
  let replacementArtifact: ArtifactRecord | null = null;

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

    await route.fulfill(problemResponse(404, "Not found.", `No fixture route for ${request.method()} ${url.pathname}.`));
  });
}

function createArtifact(issuer: string, label: string, suffix: string): ArtifactRecord {
  const secret = createBase32Secret(suffix);
  const secretUri = `otpauth://totp/${encodeURIComponent(`${issuer}:${label}`)}?secret=${secret}&issuer=${encodeURIComponent(issuer)}&algorithm=SHA1&digits=6&period=30`;
  return {
    secret,
    secretUri,
    qrCodePayload: secretUri,
  };
}

function createBase32Secret(seed: string): string {
  return Array.from({ length: 16 }, (_, index) => {
    const charCode = seed.charCodeAt(index % seed.length);
    return base32Alphabet[charCode % base32Alphabet.length];
  }).join("");
}

function toCurrentResponse(enrollment: EnrollmentRecord) {
  return {
    enrollmentId: enrollment.enrollmentId,
    tenantId: enrollment.tenantId,
    applicationClientId: enrollment.applicationClientId,
    externalUserId: enrollment.externalUserId,
    label: enrollment.label,
    status: enrollment.status,
    hasPendingReplacement: enrollment.hasPendingReplacement,
    confirmedAtUtc: enrollment.confirmedAtUtc,
    revokedAtUtc: enrollment.revokedAtUtc,
  };
}

function toCommandResponse(enrollment: EnrollmentRecord, artifact?: ArtifactRecord) {
  return {
    enrollmentId: enrollment.enrollmentId,
    status: enrollment.status,
    hasPendingReplacement: enrollment.hasPendingReplacement,
    confirmedAtUtc: enrollment.confirmedAtUtc,
    revokedAtUtc: enrollment.revokedAtUtc,
    secretUri: artifact?.secretUri ?? null,
    qrCodePayload: artifact?.qrCodePayload ?? null,
  };
}

function jsonResponse(status: number, body: unknown) {
  return {
    status,
    contentType: "application/json",
    body: JSON.stringify(body),
  };
}

function problemResponse(status: number, title: string, detail: string) {
  return jsonResponse(status, {
    type: `https://otpauth.dev/problems/${status}`,
    title,
    status,
    detail,
  });
}
