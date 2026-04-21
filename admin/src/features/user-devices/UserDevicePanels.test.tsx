import { render, screen } from "@testing-library/react";
import { useState } from "react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { AdminUserDeviceStatus, AdminUserDeviceView } from "../../shared/types/admin-contracts";
import { UserDeviceDetailPanel } from "./UserDeviceDetailPanel";
import { UserDeviceListPanel } from "./UserDeviceListPanel";
import { UserDeviceLookupPanel } from "./UserDeviceLookupPanel";

function UserDeviceLookupHarness(props: { onSubmit: () => Promise<void> }) {
  const [tenantId, setTenantId] = useState("");
  const [externalUserId, setExternalUserId] = useState("");

  return (
    <UserDeviceLookupPanel
      tenantId={tenantId}
      externalUserId={externalUserId}
      pending={false}
      canRead
      onTenantIdChange={setTenantId}
      onExternalUserIdChange={setExternalUserId}
      onSubmit={props.onSubmit}
    />
  );
}

function UserDeviceSelectionHarness(props: { devices: AdminUserDeviceView[] }) {
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null);
  const [revokeArmed, setRevokeArmed] = useState(false);
  const selectedDevice = props.devices.find((device) => device.deviceId === selectedDeviceId) ?? null;
  const onRevoke = vi.fn().mockResolvedValue(undefined);

  return (
    <>
      <UserDeviceListPanel
        devices={props.devices}
        selectedDeviceId={selectedDeviceId}
        onSelect={(device) => {
          setSelectedDeviceId(device.deviceId);
          setRevokeArmed(false);
        }}
      />
      <UserDeviceDetailPanel
        device={selectedDevice}
        loadedScope={{
          tenantId: "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb",
          externalUserId: "user-device",
        }}
        hasDraftScopeChanges={false}
        pending={false}
        canWrite
        revokeArmed={revokeArmed}
        onRevokeArmedChange={setRevokeArmed}
        onRevoke={onRevoke}
      />
    </>
  );
}

function createDevice(status: AdminUserDeviceStatus): AdminUserDeviceView {
  return {
    deviceId: "11111111-1111-1111-1111-111111111111",
    platform: "android",
    status,
    isPushCapable: true,
    activatedAtUtc: "2026-04-20T10:00:00Z",
    lastSeenAtUtc: "2026-04-20T10:05:00Z",
    revokedAtUtc: status === "revoked" ? "2026-04-20T10:10:00Z" : null,
    blockedAtUtc: status === "blocked" ? "2026-04-20T10:11:00Z" : null,
  };
}

describe("User device panels", () => {
  it("propagates device lookup changes and submit", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<UserDeviceLookupHarness onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText("Tenant ID"), "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    await user.type(screen.getByLabelText("External User ID"), "user-device");
    await user.click(screen.getByRole("button", { name: "Load devices" }));

    expect((screen.getByLabelText("Tenant ID") as HTMLInputElement).value).toBe("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    expect((screen.getByLabelText("External User ID") as HTMLInputElement).value).toBe("user-device");
    expect(onSubmit).toHaveBeenCalledOnce();
  });

  it("shows revoke confirmation only for active devices", async () => {
    const user = userEvent.setup();

    render(<UserDeviceSelectionHarness devices={[createDevice("active")]} />);

    await user.click(screen.getByRole("button", { name: "Inspect" }));
    const revokeButton = screen.getByRole("button", { name: "Revoke device" }) as HTMLButtonElement;
    expect(revokeButton.disabled).toBe(true);

    await user.click(screen.getByRole("checkbox"));
    expect(revokeButton.disabled).toBe(false);
    expect(screen.getAllByText("2026-04-20 10:05:00 UTC")).toHaveLength(2);
    expect(screen.getByText((_, element) => (
      element?.tagName === "P" &&
      (element.textContent?.includes("Loaded scope: 6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb / user-device") ?? false)
    ))).toBeTruthy();
  });

  it("keeps revoke disabled for non-active devices", async () => {
    const user = userEvent.setup();

    render(<UserDeviceSelectionHarness devices={[createDevice("blocked")]} />);

    await user.click(screen.getByRole("button", { name: "Inspect" }));

    expect((screen.getByRole("button", { name: "Revoke device" }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText("Only devices in `active` state can be revoked from this workspace.")).toBeTruthy();
  });
});
