import { expect, test } from "@playwright/test";
import { installAdminApiFixture } from "./support/admin-api-fixture";

const tenantId = "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb";
const applicationClientId = "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4";

test("operator can create, load, edit and deactivate webhook subscriptions", async ({ page }) => {
  await installAdminApiFixture(page);

  const endpointUrl = `https://crm.example.com/webhooks/${Date.now()}`;

  await page.goto("/");
  await page.getByRole("button", { name: "Open workspace" }).click();

  const lookupPanel = page.getByRole("heading", { name: "Load subscriptions" }).locator("xpath=ancestor::section[1]");
  await expect(lookupPanel).toBeVisible();
  await lookupPanel.getByLabel("Tenant ID").fill(tenantId);
  await lookupPanel.getByLabel("Application Client Filter").fill(applicationClientId);
  await lookupPanel.getByRole("button", { name: "Load subscriptions" }).click();
  await expect(page.getByText("Subscriptions loaded")).toBeVisible();

  const editorPanel = page.getByRole("heading", { name: "Save subscription" }).locator("xpath=ancestor::section[1]");
  await editorPanel.getByLabel("Application Client ID").fill(applicationClientId);
  await editorPanel.getByLabel("Webhook endpoint URL").fill(endpointUrl);
  await editorPanel.getByLabel(/device\.activated/i).check();
  await editorPanel.getByRole("button", { name: "Save subscription" }).click();

  await expect(page.getByText("Subscription saved")).toBeVisible();
  await expect(page.getByText(endpointUrl)).toBeVisible();
  await expect(page.getByText("challenge.approved, device.activated")).toBeVisible();

  await expect(page.getByRole("button", { name: "Editing" })).toBeVisible();
  await editorPanel.getByLabel("Subscription active").uncheck();
  await editorPanel.getByRole("button", { name: "Save subscription" }).click();

  await expect(page.getByText("Subscription deactivated")).toBeVisible();
  await expect(page.getByText("inactive")).toBeVisible();
});
