import { expect, test } from "@playwright/test";
import { installAdminApiFixture } from "./support/admin-api-fixture";

const tenantId = "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb";

test("operator can load user devices and revoke the active device", async ({ page }) => {
  await installAdminApiFixture(page);

  await page.goto("/");
  await page.getByRole("button", { name: "Open workspace" }).click();

  const lookupPanel = page.getByRole("heading", { name: "Load user devices" }).locator("xpath=ancestor::section[1]");
  await expect(lookupPanel).toBeVisible();
  await lookupPanel.getByLabel("Tenant ID").fill(tenantId);
  await lookupPanel.getByLabel("External User ID").fill("user-device");
  await lookupPanel.getByRole("button", { name: "Load devices" }).click();

  await expect(page.getByText("Devices loaded")).toBeVisible();
  const inventoryPanel = page.getByRole("heading", { name: "Current and recent devices" }).locator("xpath=ancestor::section[1]");
  await expect(inventoryPanel.getByText("11111111-1111-1111-1111-111111111111")).toBeVisible();
  await expect(inventoryPanel.getByText("push capable").first()).toBeVisible();

  await page.getByRole("button", { name: "Selected" }).click();
  const detailPanel = page.getByRole("heading", { name: "Selected device" }).locator("xpath=ancestor::section[1]");
  await expect(detailPanel.getByText("Loaded scope:")).toBeVisible();
  await detailPanel.getByRole("checkbox").check();
  await detailPanel.getByRole("button", { name: "Revoke device" }).click();

  await expect(page.getByText("Device revoked")).toBeVisible();
  await expect(detailPanel.getByText(/^revoked$/)).toBeVisible();
  await expect(detailPanel.getByText("2026-04-20 10:20:00 UTC")).toBeVisible();
});
