import { expect, test } from "@playwright/test";
import { generateTotpCode } from "../test-support/totp-code";
import { installAdminApiFixture } from "./support/admin-api-fixture";

const tenantId = "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb";
const applicationClientId = "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4";

test("operator flow covers login, start, confirm, replace, revoke and artifact discard", async ({ page }) => {
  await installAdminApiFixture(page);

  const externalUserId = `playwright-${Date.now()}`;
  const label = `pw-${Date.now()}`;

  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Sign in to the operator contour" })).toBeVisible();
  await page.getByRole("button", { name: "Open workspace" }).click();

  await expect(page.getByRole("button", { name: "Выйти" })).toBeVisible();
  await fillLookup(page, externalUserId);
  await page.getByLabel("Application Client ID").first().fill(applicationClientId);
  await page.getByLabel("Issuer").fill("OTPAuth Playwright");
  await page.getByLabel("Label").fill(label);
  await page.getByRole("button", { name: "Start enrollment" }).click();

  const firstSecretUri = await waitForArtifact(page);
  await confirmArtifact(page, firstSecretUri);
  await expect(page.getByText("confirmed", { exact: true })).toBeVisible();
  await expect(page.getByLabel("otpauth URI")).toHaveCount(0);

  await page.reload();
  await expect(page.getByRole("button", { name: "Выйти" })).toBeVisible();
  await fillLookup(page, externalUserId);
  await page.getByRole("button", { name: "Load current" }).click();

  await expect(page.getByText("Current state loaded")).toBeVisible();
  await expect(page.getByText("confirmed", { exact: true })).toBeVisible();
  await expect(page.getByText("yes", { exact: true })).toHaveCount(0);
  await expect(page.getByLabel("otpauth URI")).toHaveCount(0);
  expect(
    await page.evaluate(() => (globalThis as { localStorage: { length: number } }).localStorage.length),
  ).toBe(0);

  await page.getByRole("button", { name: "Start replacement" }).click();
  const replacementSecretUri = await waitForArtifact(page);
  expect(replacementSecretUri).not.toBe(firstSecretUri);
  await expect(page.getByText("yes", { exact: true })).toBeVisible();
  await confirmArtifact(page, replacementSecretUri);

  await expect(page.getByText("confirmed", { exact: true })).toBeVisible();
  await expect(page.getByText("yes", { exact: true })).toHaveCount(0);
  await expect(page.getByLabel("otpauth URI")).toHaveCount(0);

  await page.getByRole("button", { name: "Revoke enrollment" }).click();
  await expect(page.getByText("Enrollment revoked")).toBeVisible();
  await expect(page.getByText("revoked", { exact: true })).toBeVisible();
  await expect(page.getByLabel("otpauth URI")).toHaveCount(0);

  await page.getByRole("button", { name: "Load current" }).click();
  await expect(page.getByText("Current state loaded")).toBeVisible();
  await expect(page.getByText("revoked", { exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Выйти" }).click();
  await expect(page.getByRole("heading", { name: "Sign in to the operator contour" })).toBeVisible();
});

async function fillLookup(page: import("@playwright/test").Page, externalUserId: string) {
  const lookupPanel = page.getByRole("heading", { name: "Current enrollment by user" }).locator("xpath=ancestor::section[1]");
  await lookupPanel.getByLabel("Tenant ID").fill(tenantId);
  await lookupPanel.getByLabel("External User ID").fill(externalUserId);
}

async function waitForArtifact(page: import("@playwright/test").Page): Promise<string> {
  const secretUriField = page.getByLabel("otpauth URI");
  await expect(secretUriField).toBeVisible();
  const secretUri = await secretUriField.inputValue();
  expect(secretUri.startsWith("otpauth://totp/")).toBeTruthy();
  return secretUri;
}

async function confirmArtifact(page: import("@playwright/test").Page, secretUri: string) {
  const uri = new URL(secretUri);
  const secret = uri.searchParams.get("secret");
  const digits = Number.parseInt(uri.searchParams.get("digits") ?? "6", 10);
  const period = Number.parseInt(uri.searchParams.get("period") ?? "30", 10);
  const algorithm = uri.searchParams.get("algorithm") ?? "SHA1";

  expect(secret).toBeTruthy();
  const code = generateTotpCode({
    secret: secret!,
    digits,
    period,
    algorithm,
  });

  await page.getByLabel("Authenticator code").fill(code);
  await page.getByRole("button", { name: "Confirm" }).click();
}
