import { expect, test } from "@playwright/test";
import { installAdminApiFixture } from "./support/admin-api-fixture";

const tenantId = "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb";
const applicationClientId = "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4";

test("operator can load filtered delivery outcomes and inspect sanitized details", async ({ page }) => {
  await installAdminApiFixture(page);

  await page.goto("/");
  await page.getByRole("button", { name: "Open workspace" }).click();

  const lookupPanel = page.getByRole("heading", { name: "Load recent deliveries" }).locator("xpath=ancestor::section[1]");
  await expect(lookupPanel).toBeVisible();
  await lookupPanel.getByLabel("Tenant ID").fill(tenantId);
  await lookupPanel.getByLabel("Application Client Filter").fill(applicationClientId);
  await lookupPanel.getByLabel("Channel").selectOption("webhook_event");
  await lookupPanel.getByLabel("Status").selectOption("failed");
  await lookupPanel.getByLabel("Limit").fill("5");
  await lookupPanel.getByRole("button", { name: "Load deliveries" }).click();

  await expect(page.getByText("Delivery statuses loaded")).toBeVisible();
  await expect(page.getByRole("button", { name: "Selected" })).toBeVisible();
  await expect(page.locator("article").getByText("device.blocked")).toBeVisible();
  await expect(page.locator("article").getByText("https://crm.example.com/webhooks/platform")).toBeVisible();
  await expect(page.locator("article").getByText("retry scheduled")).toBeVisible();

  await page.getByRole("button", { name: "Selected" }).click();
  await expect(page.getByRole("heading", { name: "Selected outcome" })).toBeVisible();
  await expect(page.locator("section").filter({ hasText: "Selected outcome" }).getByText("delivery_failed")).toBeVisible();
  await expect(page.locator("section").filter({ hasText: "Selected outcome" }).getByText("2026-04-20 10:05:00 UTC")).toBeVisible();
  await expect(page.getByText("Operator surface intentionally excludes raw payload")).toBeVisible();
});
