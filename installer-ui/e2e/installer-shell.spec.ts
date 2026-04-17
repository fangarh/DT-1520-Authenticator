import { expect, test } from "@playwright/test";

test("runs install/update/recover happy paths through the mock installer shell", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByText(/closes the operator happy path for `install\/update\/recover`/i)).toBeVisible();

  await page.getByLabel("Env file path").fill("C:\\secure\\otpauth\\runtime.env");
  await page.getByLabel("Bootstrap admin password").fill("strong-password");
  await page.getByRole("button", { name: "Run install flow" }).click();

  await expect(page.getByText("Installer flow completed cleanly")).toBeVisible();
  await expect(page.getByRole("link", { name: "Open local Admin UI" })).toBeVisible();
  await expect(page.getByText("security_data_cleanup")).toBeVisible();

  await page.getByLabel("Update").check();
  await expect(page.getByText(/controlled rollout over an existing installation/i)).toBeVisible();
  await page.getByRole("button", { name: "Run update flow" }).click();
  await expect(page.getByText(/admin ui and worker diagnostics are healthy after the runtime restart/i)).toBeVisible();

  await page.getByLabel("Recover").check();
  await expect(page.getByText(/fast recovery after restart or partial container failure/i)).toBeVisible();
  await page.getByRole("button", { name: "Run recover flow" }).click();
  await expect(page.getByText(/confirm the previously installed runtime services are healthy again/i)).toBeVisible();
});
