import { afterEach, describe, expect, it, vi } from "vitest";
import { installerApi, InstallerApiError } from "./installer-api";

describe("installerApi", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("posts the installer request as JSON", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(JSON.stringify({
      report: {
        outcome: "succeeded",
      },
    }), { status: 200 }));

    await installerApi.run({
      mode: "Install",
      envFilePath: "C:\\secure\\runtime.env",
      composeFilePath: "C:\\compose.yml",
      bootstrapAdminUsername: "operator",
      bootstrapAdminPermissions: ["enrollments.read"],
      bootstrapAdminPassword: "secret",
      preflightOnly: false,
      dryRun: true,
      skipImageBuild: false,
      skipBootstrap: false,
      skipBootstrapAdmin: false,
      skipPortAvailabilityCheck: false,
    });

    expect(fetchMock).toHaveBeenCalledWith("/api/run", expect.objectContaining({
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
    }));
  });

  it("surfaces bridge error details", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(JSON.stringify({
      title: "Installer bridge rejected the request",
      detail: "Env file path is required.",
    }), { status: 400 }));

    await expect(installerApi.run({
      mode: "Install",
      envFilePath: "",
      bootstrapAdminUsername: "operator",
      bootstrapAdminPermissions: ["enrollments.read"],
      preflightOnly: false,
      dryRun: false,
      skipImageBuild: false,
      skipBootstrap: false,
      skipBootstrapAdmin: false,
      skipPortAvailabilityCheck: false,
    })).rejects.toBeInstanceOf(InstallerApiError);
  });
});
