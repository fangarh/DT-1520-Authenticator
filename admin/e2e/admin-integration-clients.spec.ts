import { expect, test } from "@playwright/test";
import { installAdminApiFixture } from "./support/admin-api-fixture";

const tenantId = "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb";
const applicationClientId = "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4";

test("operator can load and create integration client with one-time secret display", async ({ page }) => {
  await installAdminApiFixture(page, { includeTenantPermissions: false });

  const clientId = `playwright-client-${Date.now()}`;

  await page.goto("/");
  await page.getByRole("button", { name: "Open workspace" }).click();

  const lookupPanel = page.getByRole("heading", { name: "Load clients" }).locator("xpath=ancestor::section[1]");
  await lookupPanel.getByLabel("Tenant ID").fill(tenantId);
  await lookupPanel.getByRole("button", { name: "Load clients" }).click();

  await expect(page.getByText("Integration clients loaded")).toBeVisible();
  await expect(page.getByRole("article").filter({ hasText: "project-manager-pilot" })).toBeVisible();

  const createPanel = page.getByRole("heading", { name: "Create integration client" }).locator("xpath=ancestor::section[1]");
  await createPanel.getByLabel("Tenant ID").fill(tenantId);
  await createPanel.getByLabel("Application Client ID").fill(applicationClientId);
  await createPanel.getByLabel("Client ID", { exact: true }).fill(clientId);
  await createPanel.getByLabel(/enrollments:write/i).check();
  await createPanel.getByRole("button", { name: "Create client" }).click();

  const oneTimeSecret = `fixture-secret-${clientId}`;
  await expect(page.getByText("Integration client created")).toBeVisible();
  await expect(createPanel.getByText(oneTimeSecret)).toBeVisible();
  await expect(page.getByRole("article").filter({ hasText: clientId })).toBeVisible();

  await createPanel.getByRole("button", { name: "Discard secret" }).click();
  await expect(page.getByText("Secret discarded")).toBeVisible();
  await expect(page.getByText(oneTimeSecret)).toHaveCount(0);

  expect(
    await page.evaluate(() => (globalThis as { localStorage: { length: number }; sessionStorage: { length: number } }).localStorage.length),
  ).toBe(0);
  expect(
    await page.evaluate(() => (globalThis as { localStorage: { length: number }; sessionStorage: { length: number } }).sessionStorage.length),
  ).toBe(0);

  await page.reload();
  await expect(page.getByRole("button", { name: "Выйти" })).toBeVisible();
  await expect(page.getByText(oneTimeSecret)).toHaveCount(0);
});

test("operator can run integration client lifecycle actions", async ({ page }) => {
  await installAdminApiFixture(page, { includeTenantPermissions: false });

  await page.goto("/");
  await page.getByRole("button", { name: "Open workspace" }).click();

  const lookupPanel = page.getByRole("heading", { name: "Load clients" }).locator("xpath=ancestor::section[1]");
  await lookupPanel.getByLabel("Tenant ID").fill(tenantId);
  await lookupPanel.getByRole("button", { name: "Load clients" }).click();

  const lifecyclePanel = page.getByRole("heading", { name: "Manage selected client" }).locator("xpath=ancestor::section[1]");
  await expect(lifecyclePanel.locator("strong").filter({ hasText: "project-manager-pilot" }).first()).toBeVisible();

  await lifecyclePanel.getByLabel(/previous client secret will stop working/i).check();
  await lifecyclePanel.getByRole("button", { name: "Rotate secret" }).click();
  await expect(page.getByText("Secret rotated")).toBeVisible();
  await expect(lifecyclePanel.getByText("fixture-rotated-secret-project-manager-pilot")).toBeVisible();
  await lifecyclePanel.getByRole("button", { name: "Discard secret" }).click();
  await expect(lifecyclePanel.getByText("fixture-rotated-secret-project-manager-pilot")).toHaveCount(0);

  await lifecyclePanel.getByLabel(/devices:write/i).check();
  await lifecyclePanel.getByRole("button", { name: "Save scopes" }).click();
  await expect(page.getByText("Scopes updated")).toBeVisible();
  await expect(page.getByText(/challenges:read, challenges:write, devices:write/).first()).toBeVisible();

  await lifecyclePanel.getByLabel(/invalidates issued tokens/i).check();
  await lifecyclePanel.getByRole("button", { name: "Deactivate client" }).click();
  await expect(page.getByText("Client deactivated")).toBeVisible();
  await expect(lifecyclePanel.getByText("inactive").first()).toBeVisible();
  await expect(lifecyclePanel.getByRole("button", { name: "Rotate secret" })).toBeDisabled();
  await expect(lifecyclePanel.getByRole("button", { name: "Deactivate client" })).toBeDisabled();

  await lifecyclePanel.getByLabel(/receive tokens again/i).check();
  await lifecyclePanel.getByRole("button", { name: "Reactivate client" }).click();
  await expect(page.getByText("Client reactivated")).toBeVisible();
  await expect(lifecyclePanel.getByText("active").first()).toBeVisible();
});
