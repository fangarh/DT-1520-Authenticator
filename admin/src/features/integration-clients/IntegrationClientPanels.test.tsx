import { render, screen } from "@testing-library/react";
import { useState } from "react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { AdminIntegrationClientScope, AdminIntegrationClientView } from "../../shared/types/admin-contracts";
import { IntegrationClientCreatePanel } from "./IntegrationClientCreatePanel";
import { IntegrationClientLifecyclePanel } from "./IntegrationClientLifecyclePanel";
import { IntegrationClientListPanel } from "./IntegrationClientListPanel";

function CreatePanelHarness(props: { onCreate: () => Promise<void> }) {
  const [tenantId, setTenantId] = useState("");
  const [applicationClientId, setApplicationClientId] = useState("");
  const [clientId, setClientId] = useState("");
  const [allowedScopes, setAllowedScopes] = useState<AdminIntegrationClientScope[]>(["challenges:read"]);

  function toggleScope(scope: AdminIntegrationClientScope) {
    setAllowedScopes((current) => (
      current.includes(scope)
        ? current.filter((item) => item !== scope)
        : [...current, scope]
    ));
  }

  return (
    <IntegrationClientCreatePanel
      tenantId={tenantId}
      applicationClientId={applicationClientId}
      clientId={clientId}
      allowedScopes={allowedScopes}
      oneTimeSecret={null}
      pending={null}
      canWrite
      onTenantIdChange={setTenantId}
      onApplicationClientIdChange={setApplicationClientId}
      onClientIdChange={setClientId}
      onToggleScope={toggleScope}
      onCreate={props.onCreate}
      onCopySecret={vi.fn()}
      onDiscardSecret={vi.fn()}
      onReset={vi.fn()}
    />
  );
}

describe("Integration client panels", () => {
  const activeClient: AdminIntegrationClientView = {
    clientId: "project-manager",
    tenantId: "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb",
    applicationClientId: "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4",
    status: "active",
    allowedScopes: ["challenges:read", "challenges:write"],
    createdUtc: "2026-04-27T10:00:00Z",
    updatedUtc: null,
    lastSecretRotatedUtc: null,
    lastAuthStateChangedUtc: "2026-04-27T10:00:00Z",
  };

  it("propagates create form changes and submit", async () => {
    const user = userEvent.setup();
    const onCreate = vi.fn().mockResolvedValue(undefined);

    render(<CreatePanelHarness onCreate={onCreate} />);

    await user.type(screen.getByLabelText("Tenant ID"), "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    await user.type(screen.getByLabelText("Application Client ID"), "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");
    await user.type(screen.getByLabelText("Client ID"), "project-manager");
    await user.click(screen.getByLabelText(/challenges:write/i));
    await user.click(screen.getByRole("button", { name: "Create client" }));

    expect((screen.getByLabelText("Tenant ID") as HTMLInputElement).value).toBe("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    expect((screen.getByLabelText("Application Client ID") as HTMLInputElement).value).toBe("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");
    expect((screen.getByLabelText("Client ID") as HTMLInputElement).value).toBe("project-manager");
    expect((screen.getByLabelText(/challenges:write/i) as HTMLInputElement).checked).toBe(true);
    expect(onCreate).toHaveBeenCalledOnce();
  });

  it("renders and discards one-time secret without hidden persistence", async () => {
    const user = userEvent.setup();
    const onDiscardSecret = vi.fn();

    render(
      <IntegrationClientCreatePanel
        tenantId=""
        applicationClientId=""
        clientId=""
        allowedScopes={["challenges:read"]}
        oneTimeSecret={{ clientId: "project-manager", clientSecret: "one-time-secret-value" }}
        pending={null}
        canWrite
        onTenantIdChange={vi.fn()}
        onApplicationClientIdChange={vi.fn()}
        onClientIdChange={vi.fn()}
        onToggleScope={vi.fn()}
        onCreate={vi.fn()}
        onCopySecret={vi.fn()}
        onDiscardSecret={onDiscardSecret}
        onReset={vi.fn()}
      />,
    );

    expect(screen.getByText("one-time-secret-value")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Discard secret" }));

    expect(onDiscardSecret).toHaveBeenCalledOnce();
  });

  it("forwards selected client from inventory list", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(
      <IntegrationClientListPanel
        clients={[activeClient]}
        selectedClientId={null}
        onSelect={onSelect}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Inspect client" }));

    expect(screen.getByText("project-manager")).toBeTruthy();
    expect(onSelect).toHaveBeenCalledWith(activeClient);
  });

  it("requires confirmation before rotating a client secret", async () => {
    const user = userEvent.setup();
    const onRotateSecret = vi.fn().mockResolvedValue(undefined);
    const onRotateArmedChange = vi.fn();

    render(
      <IntegrationClientLifecyclePanel
        client={activeClient}
        scopeDraft={activeClient.allowedScopes}
        hasScopeChanges={false}
        rotatedSecret={null}
        pending={null}
        canWrite
        rotateArmed={false}
        deactivateArmed={false}
        reactivateArmed={false}
        onToggleScope={vi.fn()}
        onUpdateScopes={vi.fn()}
        onRotateSecret={onRotateSecret}
        onDeactivate={vi.fn()}
        onReactivate={vi.fn()}
        onCopyRotatedSecret={vi.fn()}
        onDiscardRotatedSecret={vi.fn()}
        onRotateArmedChange={onRotateArmedChange}
        onDeactivateArmedChange={vi.fn()}
        onReactivateArmedChange={vi.fn()}
      />,
    );

    expect((screen.getByRole("button", { name: "Rotate secret" }) as HTMLButtonElement).disabled).toBe(true);
    await user.click(screen.getByLabelText(/previous client secret will stop working/i));

    expect(onRotateArmedChange).toHaveBeenCalledWith(true);
    expect(onRotateSecret).not.toHaveBeenCalled();
  });

  it("updates lifecycle scope selection and submit", async () => {
    const user = userEvent.setup();
    const onToggleScope = vi.fn();
    const onUpdateScopes = vi.fn().mockResolvedValue(undefined);

    render(
      <IntegrationClientLifecyclePanel
        client={activeClient}
        scopeDraft={["challenges:read"]}
        hasScopeChanges
        rotatedSecret={null}
        pending={null}
        canWrite
        rotateArmed={false}
        deactivateArmed={false}
        reactivateArmed={false}
        onToggleScope={onToggleScope}
        onUpdateScopes={onUpdateScopes}
        onRotateSecret={vi.fn()}
        onDeactivate={vi.fn()}
        onReactivate={vi.fn()}
        onCopyRotatedSecret={vi.fn()}
        onDiscardRotatedSecret={vi.fn()}
        onRotateArmedChange={vi.fn()}
        onDeactivateArmedChange={vi.fn()}
        onReactivateArmedChange={vi.fn()}
      />,
    );

    await user.click(screen.getByLabelText(/devices:write/i));
    await user.click(screen.getByRole("button", { name: "Save scopes" }));

    expect(onToggleScope).toHaveBeenCalledWith("devices:write");
    expect(onUpdateScopes).toHaveBeenCalledOnce();
  });

  it("disables deactivate for inactive clients and enables reactivate confirmation", async () => {
    const user = userEvent.setup();
    const onReactivateArmedChange = vi.fn();
    const inactiveClient: AdminIntegrationClientView = {
      ...activeClient,
      status: "inactive",
    };

    render(
      <IntegrationClientLifecyclePanel
        client={inactiveClient}
        scopeDraft={inactiveClient.allowedScopes}
        hasScopeChanges={false}
        rotatedSecret={null}
        pending={null}
        canWrite
        rotateArmed={false}
        deactivateArmed={false}
        reactivateArmed={false}
        onToggleScope={vi.fn()}
        onUpdateScopes={vi.fn()}
        onRotateSecret={vi.fn()}
        onDeactivate={vi.fn()}
        onReactivate={vi.fn()}
        onCopyRotatedSecret={vi.fn()}
        onDiscardRotatedSecret={vi.fn()}
        onRotateArmedChange={vi.fn()}
        onDeactivateArmedChange={vi.fn()}
        onReactivateArmedChange={onReactivateArmedChange}
      />,
    );

    expect((screen.getByRole("button", { name: "Deactivate client" }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole("button", { name: "Reactivate client" }) as HTMLButtonElement).disabled).toBe(true);
    await user.click(screen.getByLabelText(/receive tokens again/i));

    expect(onReactivateArmedChange).toHaveBeenCalledWith(true);
  });

  it("renders and discards rotated one-time secret", async () => {
    const user = userEvent.setup();
    const onDiscardRotatedSecret = vi.fn();

    render(
      <IntegrationClientLifecyclePanel
        client={activeClient}
        scopeDraft={activeClient.allowedScopes}
        hasScopeChanges={false}
        rotatedSecret={{ clientId: "project-manager", clientSecret: "rotated-one-time-secret" }}
        pending={null}
        canWrite
        rotateArmed={false}
        deactivateArmed={false}
        reactivateArmed={false}
        onToggleScope={vi.fn()}
        onUpdateScopes={vi.fn()}
        onRotateSecret={vi.fn()}
        onDeactivate={vi.fn()}
        onReactivate={vi.fn()}
        onCopyRotatedSecret={vi.fn()}
        onDiscardRotatedSecret={onDiscardRotatedSecret}
        onRotateArmedChange={vi.fn()}
        onDeactivateArmedChange={vi.fn()}
        onReactivateArmedChange={vi.fn()}
      />,
    );

    expect(screen.getByText("rotated-one-time-secret")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Discard secret" }));

    expect(onDiscardRotatedSecret).toHaveBeenCalledOnce();
  });
});
