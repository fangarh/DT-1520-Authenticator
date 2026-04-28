import { expect, test } from "@playwright/test";
import { installAdminApiFixture } from "./support/admin-api-fixture";

const tenantId = "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb";
const applicationClientId = "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4";

test("operator can create QR onboarding artifact and revoke pending artifact", async ({ page }) => {
  await installAdminApiFixture(page);

  await page.goto("/");
  await page.getByRole("button", { name: "Open workspace" }).click();

  const lookupPanel = page.getByRole("heading", { name: "Load onboarding artifacts" }).locator("xpath=ancestor::section[1]");
  await lookupPanel.getByLabel("Tenant ID").fill(tenantId);
  await lookupPanel.getByLabel("External User ID").fill("user-device");
  await lookupPanel.getByLabel("Application Client ID").fill(applicationClientId);
  await lookupPanel.getByRole("button", { name: "Load QR artifacts" }).click();

  await expect(page.getByText("Onboarding artifacts loaded")).toBeVisible();
  await expect(page.getByRole("article").filter({ hasText: "44444444-4444-4444-4444-444444444444" })).toBeVisible();
  await expect(page.getByText("fixture-existing-payload-hidden-from-list")).toHaveCount(0);

  const createPanel = page.getByRole("heading", { name: "Issue device QR" }).locator("xpath=ancestor::section[1]");
  await createPanel.getByLabel("Tenant ID").fill(tenantId);
  await createPanel.getByLabel("Application Client ID").fill(applicationClientId);
  await createPanel.getByLabel("External User ID").fill("user-device");
  await createPanel.getByLabel("TTL minutes").fill("15");
  await createPanel.getByRole("button", { name: "Create QR" }).click();

  await expect(page.getByText("QR artifact created")).toBeVisible();
  const createdPayload = await page.locator("code").filter({ hasText: "fixture-activation-payload-" }).innerText();
  await expect(page.getByLabel("One-time device activation QR")).toBeVisible();
  expect(await page.evaluate("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).toBe(true);

  const qrPanel = page.getByRole("heading", { name: "QR for mobile activation" }).locator("xpath=ancestor::section[1]");
  await qrPanel.getByRole("button", { name: "Discard payload" }).click();
  await expect(page.getByText("Payload discarded")).toBeVisible();
  await expect(page.getByText(createdPayload)).toHaveCount(0);

  const detailPanel = page.getByRole("heading", { name: "Sanitized metadata" }).last().locator("xpath=ancestor::section[1]");
  await detailPanel.getByLabel(/prevent this QR from activating/i).check();
  await detailPanel.getByRole("button", { name: "Revoke QR" }).click();
  await expect(page.getByText("QR artifact revoked")).toBeVisible();
  await expect(detailPanel.locator("span").filter({ hasText: /^revoked$/ })).toBeVisible();

  expect(await page.evaluate(() => localStorage.length)).toBe(0);
  expect(await page.evaluate(() => sessionStorage.length)).toBe(0);

  await page.reload();
  await expect(page.getByRole("button", { name: "Выйти" })).toBeVisible();
  await expect(page.getByText(createdPayload)).toHaveCount(0);
});
