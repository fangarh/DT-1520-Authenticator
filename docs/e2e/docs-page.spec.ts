import { expect, test } from "@playwright/test";

test("renders standalone documentation page", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Документация MVP runtime" })).toBeVisible();
  await expect(page.locator("#admin-title")).toBeVisible();
  await expect(page.getByText("mobile/app/src/debug/PilotDeviceActivationActivity.kt")).toBeVisible();

  const overflowSources = await page.evaluate(() => {
    const viewportWidth = document.documentElement.clientWidth;
    return Array.from(document.querySelectorAll("body *"))
      .map((element) => {
        const rect = element.getBoundingClientRect();
        return {
          tagName: element.tagName,
          text: element.textContent?.trim().slice(0, 80),
          right: Math.round(rect.right),
          width: Math.round(rect.width),
        };
      })
      .filter((entry) => entry.right > viewportWidth + 1);
  });
  expect(overflowSources).toEqual([]);
});
