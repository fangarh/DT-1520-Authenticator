import { render, screen } from "@testing-library/react";
import { useState } from "react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { AdminDeviceOnboardingArtifactView } from "../../shared/types/admin-contracts";
import { DeviceOnboardingCreatePanel } from "./DeviceOnboardingCreatePanel";
import { DeviceOnboardingDetailPanel } from "./DeviceOnboardingDetailPanel";
import { DeviceOnboardingListPanel } from "./DeviceOnboardingListPanel";
import { DeviceOnboardingLookupPanel } from "./DeviceOnboardingLookupPanel";
import { DeviceOnboardingQrPanel } from "./DeviceOnboardingQrPanel";

vi.mock("qrcode.react", () => ({
  QRCodeSVG: (props: { value: string; title?: string; role?: string; "aria-label"?: string }) => (
    <svg data-qr-value={props.value} role={props.role} aria-label={props["aria-label"]}>
      <title>{props.title}</title>
    </svg>
  ),
}));

function LookupHarness(props: { onSubmit: () => Promise<void> }) {
  const [tenantId, setTenantId] = useState("");
  const [externalUserId, setExternalUserId] = useState("");
  const [applicationClientId, setApplicationClientId] = useState("");
  const [limit, setLimit] = useState("50");

  return (
    <DeviceOnboardingLookupPanel
      tenantId={tenantId}
      externalUserId={externalUserId}
      applicationClientId={applicationClientId}
      status="pending"
      limit={limit}
      pending={false}
      canRead
      onTenantIdChange={setTenantId}
      onExternalUserIdChange={setExternalUserId}
      onApplicationClientIdChange={setApplicationClientId}
      onStatusChange={vi.fn()}
      onLimitChange={setLimit}
      onSubmit={props.onSubmit}
    />
  );
}

function createArtifact(status: AdminDeviceOnboardingArtifactView["status"]): AdminDeviceOnboardingArtifactView {
  return {
    activationCodeId: "11111111-1111-1111-1111-111111111111",
    tenantId: "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb",
    applicationClientId: "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4",
    externalUserId: "user-device",
    platform: "android",
    status,
    expiresAtUtc: "2026-04-27T10:15:00Z",
    consumedAtUtc: null,
    revokedAtUtc: status === "revoked" ? "2026-04-27T10:05:00Z" : null,
    createdAtUtc: "2026-04-27T10:00:00Z",
  };
}

describe("Device onboarding panels", () => {
  it("propagates lookup changes and submit", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<LookupHarness onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText("Tenant ID"), "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    await user.type(screen.getByLabelText("External User ID"), "user-device");
    await user.type(screen.getByLabelText("Application Client ID"), "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");
    await user.clear(screen.getByLabelText("Limit"));
    await user.type(screen.getByLabelText("Limit"), "25");
    await user.click(screen.getByRole("button", { name: "Load QR artifacts" }));

    expect((screen.getByLabelText("Tenant ID") as HTMLInputElement).value).toBe("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    expect((screen.getByLabelText("External User ID") as HTMLInputElement).value).toBe("user-device");
    expect((screen.getByLabelText("Application Client ID") as HTMLInputElement).value).toBe("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");
    expect((screen.getByLabelText("Limit") as HTMLInputElement).value).toBe("25");
    expect(onSubmit).toHaveBeenCalledOnce();
  });

  it("propagates create form changes and submit", async () => {
    const user = userEvent.setup();
    const onCreate = vi.fn().mockResolvedValue(undefined);

    render(
      <DeviceOnboardingCreatePanel
        tenantId=""
        applicationClientId=""
        externalUserId=""
        platform="android"
        ttlMinutes="15"
        pending={null}
        canWrite
        onTenantIdChange={vi.fn()}
        onApplicationClientIdChange={vi.fn()}
        onExternalUserIdChange={vi.fn()}
        onPlatformChange={vi.fn()}
        onTtlMinutesChange={vi.fn()}
        onCreate={onCreate}
        onReset={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Create QR" }));

    expect(onCreate).toHaveBeenCalledOnce();
  });

  it("renders QR and discards one-time activation payload", async () => {
    const user = userEvent.setup();
    const onDiscard = vi.fn();
    localStorage.clear();
    sessionStorage.clear();

    render(
      <DeviceOnboardingQrPanel
        payload={{
          activationCodeId: "11111111-1111-1111-1111-111111111111",
          activationPayload: "dac_11111111-1111-1111-1111-111111111111.secret",
          runtimeBaseUrl: "https://admin.ghostring.ru:18443",
          expiresAtUtc: "2026-04-27T10:15:00Z",
        }}
        pending={null}
        onCopy={vi.fn()}
        onDiscard={onDiscard}
      />,
    );

    const qr = screen.getByLabelText("One-time device activation QR");
    const qrEnvelope = JSON.parse(qr.getAttribute("data-qr-value") ?? "{}") as {
      v?: number;
      runtimeBaseUrl?: string;
      activationPayload?: string;
    };

    expect(qrEnvelope).toEqual({
      v: 1,
      runtimeBaseUrl: "https://admin.ghostring.ru:18443",
      activationPayload: "dac_11111111-1111-1111-1111-111111111111.secret",
    });
    expect(screen.getByText("dac_11111111-1111-1111-1111-111111111111.secret")).toBeTruthy();
    expect(screen.getByText("Runtime: https://admin.ghostring.ru:18443")).toBeTruthy();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);

    await user.click(screen.getByRole("button", { name: "Discard payload" }));

    expect(onDiscard).toHaveBeenCalledOnce();
  });

  it("forwards artifact selection from inventory list", async () => {
    const user = userEvent.setup();
    const artifact = createArtifact("pending");
    const onSelect = vi.fn();

    render(
      <DeviceOnboardingListPanel
        artifacts={[artifact]}
        selectedArtifactId={null}
        onSelect={onSelect}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Inspect QR" }));

    expect(screen.getByText(artifact.activationCodeId)).toBeTruthy();
    expect(onSelect).toHaveBeenCalledWith(artifact);
  });

  it("requires confirmation before revoking pending artifact", async () => {
    const user = userEvent.setup();
    const onRevokeArmedChange = vi.fn();

    render(
      <DeviceOnboardingDetailPanel
        artifact={createArtifact("pending")}
        pending={false}
        canWrite
        revokeArmed={false}
        onRevokeArmedChange={onRevokeArmedChange}
        onRevoke={vi.fn()}
      />,
    );

    expect((screen.getByRole("button", { name: "Revoke QR" }) as HTMLButtonElement).disabled).toBe(true);
    await user.click(screen.getByLabelText(/prevent this QR from activating/i));

    expect(onRevokeArmedChange).toHaveBeenCalledWith(true);
  });

  it("keeps revoke disabled for non-pending artifacts", () => {
    render(
      <DeviceOnboardingDetailPanel
        artifact={createArtifact("revoked")}
        pending={false}
        canWrite
        revokeArmed
        onRevokeArmedChange={vi.fn()}
        onRevoke={vi.fn()}
      />,
    );

    expect((screen.getByRole("button", { name: "Revoke QR" }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText("Only `pending` QR artifacts can be revoked from this workspace.")).toBeTruthy();
  });
});
