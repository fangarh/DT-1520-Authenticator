import { expect, test } from "@playwright/test";
import { installAdminApiFixture } from "./support/admin-api-fixture";

test("operator can load tenant directory and quick-create first API client", async ({ page }) => {
  await installAdminApiFixture(page);

  const uniqueSuffix = Date.now();
  const tenantName = `Playwright Tenant ${uniqueSuffix}`;

  await page.goto("/");
  await page.getByRole("button", { name: "Open workspace" }).click();

  const directoryPanel = page.getByRole("heading", { name: "Organizations" }).locator("xpath=ancestor::section[1]");
  await directoryPanel.getByRole("button", { name: "Load tenants" }).click();

  await expect(page.getByText("Tenants loaded")).toBeVisible();
  await expect(page.getByText("Directory Tenant")).toBeVisible();
  await directoryPanel.getByRole("button", { name: "Open" }).first().click();
  await expect(page.getByText("Tenant selected")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Directory Tenant", exact: true })).toBeVisible();
  await expect(page.getByText("project-manager-pilot")).toBeVisible();

  const quickCreatePanel = page.getByRole("heading", { name: "Quick create tenant" }).locator("xpath=ancestor::section[1]");
  await quickCreatePanel.getByLabel("Tenant display name").fill(tenantName);
  await quickCreatePanel.getByLabel("Application display name").fill("Reference Backend");
  await quickCreatePanel.getByLabel("API client display name").fill("Desktop API");
  await quickCreatePanel.getByLabel(/enrollments:write/i).uncheck();
  await quickCreatePanel.getByRole("button", { name: "Quick create" }).click();

  await expect(page.getByText("Tenant quick-created")).toBeVisible();
  await expect(page.getByRole("heading", { name: tenantName, exact: true })).toBeVisible();

  const secret = page.getByText(/fixture-tenant-secret-/);
  await expect(secret).toBeVisible();
  await quickCreatePanel.getByRole("button", { name: "Discard secret" }).click();
  await expect(page.getByText("Secret discarded")).toBeVisible();
  await expect(secret).toHaveCount(0);

  expect(await page.evaluate(() => localStorage.length)).toBe(0);
  expect(await page.evaluate(() => sessionStorage.length)).toBe(0);

  await page.reload();
  await expect(page.getByRole("button", { name: "Выйти" })).toBeVisible();
  await expect(page.getByText(/fixture-tenant-secret-/)).toHaveCount(0);
});

test("operator can manage selected tenant clients, user devices, runtime and reports", async ({ page }) => {
  await installAdminApiFixture(page);

  await page.goto("/");
  await page.getByRole("button", { name: "Open workspace" }).click();

  const directoryPanel = page.getByRole("heading", { name: "Organizations" }).locator("xpath=ancestor::section[1]");
  await directoryPanel.getByRole("button", { name: "Load tenants" }).click();
  await directoryPanel.getByRole("button", { name: "Open" }).first().click();

  const managementPanel = page.getByRole("heading", { name: "Directory Tenant operations" }).locator("xpath=ancestor::section[1]");
  await managementPanel.getByRole("tab", { name: "API clients" }).click();
  await managementPanel.getByLabel("Client ID").fill("tenant-managed-client");
  await managementPanel.getByRole("button", { name: "Create client in selected tenant" }).click();
  await expect(managementPanel.getByText("fixture-secret-tenant-managed-client")).toBeVisible();
  await managementPanel.getByLabel("Rotate client secret and invalidate the old secret.").check();
  await managementPanel.getByRole("button", { name: "Rotate secret" }).click();
  await expect(managementPanel.getByText("fixture-rotated-secret-tenant-managed-client")).toBeVisible();
  await managementPanel.getByRole("button", { name: "Discard secret" }).click();
  await expect(managementPanel.getByText(/fixture-rotated-secret-/)).toHaveCount(0);

  await managementPanel.getByRole("tab", { name: "Users & devices" }).click();
  await managementPanel.getByLabel("External User ID").fill("user-device");
  await managementPanel.getByRole("button", { name: "Load user devices" }).click();
  await expect(managementPanel.getByText("11111111-1111-1111-1111-111111111111")).toBeVisible();
  await managementPanel.getByRole("button", { name: "Issue QR for selected user" }).click();
  await expect(managementPanel.getByText(/dac_.*fixture-secret/)).toBeVisible();
  await managementPanel.getByRole("button", { name: "Discard payload" }).click();
  await expect(managementPanel.getByText(/dac_.*fixture-secret/)).toHaveCount(0);

  await managementPanel.getByRole("tab", { name: "Runtime" }).click();
  await managementPanel.getByRole("button", { name: "Load runtime configuration" }).click();
  await expect(managementPanel.getByText("PrivateNetwork", { exact: true })).toBeVisible();

  await managementPanel.getByRole("tab", { name: "Reports" }).click();
  await managementPanel.getByRole("button", { name: "Refresh report snapshot" }).click();
  await expect(managementPanel.getByText("challenge.approved / delivered")).toBeVisible();
  await expect(managementPanel.getByText("QR pending")).toBeVisible();
  await expect(page.getByRole("heading", { name: "TOTP enrollment workspace" })).toHaveCount(0);

  expect(await page.evaluate(() => localStorage.length)).toBe(0);
  expect(await page.evaluate(() => sessionStorage.length)).toBe(0);
});

test("operator can use advanced manual tenant create without secret display", async ({ page }) => {
  await installAdminApiFixture(page);

  await page.goto("/");
  await page.getByRole("button", { name: "Open workspace" }).click();

  const manualPanel = page.getByRole("heading", { name: "Manual tenant create" }).locator("xpath=ancestor::section[1]");
  await manualPanel.getByLabel("Tenant ID").fill("30303030-3030-3030-3030-303030303030");
  await manualPanel.getByLabel("Display name").fill("Manual Migration Tenant");
  await manualPanel.getByLabel("Slug").fill("manual-migration-tenant");
  await manualPanel.getByLabel("Status").selectOption("test");
  await manualPanel.getByRole("button", { name: "Create tenant" }).click();

  await expect(page.getByText("Tenant created", { exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Manual Migration Tenant", exact: true })).toBeVisible();
  await expect(page.getByText("30303030-3030-3030-3030-303030303030").first()).toBeVisible();
  await expect(page.getByText(/fixture-tenant-secret-/)).toHaveCount(0);
});

test("tenant directory remains usable on narrow viewport", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 900 });
  await installAdminApiFixture(page);

  await page.goto("/");
  await page.getByRole("button", { name: "Open workspace" }).click();

  const directoryPanel = page.getByRole("heading", { name: "Organizations" }).locator("xpath=ancestor::section[1]");
  await directoryPanel.getByRole("button", { name: "Load tenants" }).click();
  await expect(page.getByText("Directory Tenant")).toBeVisible();

  await expect.poll(async () => (
    directoryPanel.evaluate((element) => element.scrollWidth <= element.clientWidth + 1)
  )).toBe(true);

  const managementPanel = page.getByRole("heading", { name: "Selected tenant operations" }).locator("xpath=ancestor::section[1]");
  await expect.poll(async () => (
    managementPanel.evaluate((element) => element.scrollWidth <= element.clientWidth + 1)
  )).toBe(true);
});
