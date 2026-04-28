import { render, screen } from "@testing-library/react";
import { useState } from "react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type {
  AdminIntegrationClientScope,
  AdminTenantDirectoryStatus,
  AdminTenantDirectoryTenantView,
} from "../../shared/types/admin-contracts";
import { TenantDirectoryListPanel } from "./TenantDirectoryListPanel";
import { TenantManualCreatePanel } from "./TenantManualCreatePanel";
import { TenantQuickCreatePanel } from "./TenantQuickCreatePanel";

function QuickCreateHarness(props: { onQuickCreate: () => Promise<void> }) {
  const [tenantDisplayName, setTenantDisplayName] = useState("");
  const [applicationDisplayName, setApplicationDisplayName] = useState("");
  const [integrationClientDisplayName, setIntegrationClientDisplayName] = useState("");
  const [allowedScopes, setAllowedScopes] = useState<AdminIntegrationClientScope[]>(["challenges:read"]);

  function toggleScope(scope: AdminIntegrationClientScope) {
    setAllowedScopes((current) => (
      current.includes(scope)
        ? current.filter((item) => item !== scope)
        : [...current, scope]
    ));
  }

  return (
    <TenantQuickCreatePanel
      tenantDisplayName={tenantDisplayName}
      applicationDisplayName={applicationDisplayName}
      integrationClientDisplayName={integrationClientDisplayName}
      allowedScopes={allowedScopes}
      oneTimeSecret={null}
      pending={null}
      canWrite
      onTenantDisplayNameChange={setTenantDisplayName}
      onApplicationDisplayNameChange={setApplicationDisplayName}
      onIntegrationClientDisplayNameChange={setIntegrationClientDisplayName}
      onToggleScope={toggleScope}
      onQuickCreate={props.onQuickCreate}
      onCopySecret={vi.fn()}
      onDiscardSecret={vi.fn()}
      onReset={vi.fn()}
    />
  );
}

function ManualCreateHarness(props: { onCreate: () => Promise<void> }) {
  const [tenantId, setTenantId] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [slug, setSlug] = useState("");
  const [status, setStatus] = useState<AdminTenantDirectoryStatus>("active");

  return (
    <TenantManualCreatePanel
      tenantId={tenantId}
      displayName={displayName}
      slug={slug}
      status={status}
      pending={null}
      canWrite
      onTenantIdChange={setTenantId}
      onDisplayNameChange={setDisplayName}
      onSlugChange={setSlug}
      onStatusChange={setStatus}
      onCreate={props.onCreate}
      onReset={vi.fn()}
    />
  );
}

describe("Tenant directory panels", () => {
  const tenant: AdminTenantDirectoryTenantView = {
    tenantId: "11111111-1111-1111-1111-111111111111",
    displayName: "Directory Tenant",
    slug: "directory-tenant",
    status: "active",
    applicationCount: 1,
    integrationClientCount: 1,
    createdUtc: "2026-04-28T10:00:00Z",
    updatedUtc: "2026-04-28T10:10:00Z",
  };

  it("loads and selects tenants from inventory", async () => {
    const user = userEvent.setup();
    const onLoad = vi.fn().mockResolvedValue(undefined);
    const onSelect = vi.fn().mockResolvedValue(undefined);

    render(
      <TenantDirectoryListPanel
        tenants={[tenant]}
        selectedTenantId={null}
        pending={false}
        canRead
        onLoad={onLoad}
        onSelect={onSelect}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Load tenants" }));
    await user.click(screen.getByRole("button", { name: "Open" }));

    expect(screen.getByText("Directory Tenant")).toBeTruthy();
    expect(onLoad).toHaveBeenCalledOnce();
    expect(onSelect).toHaveBeenCalledWith(tenant);
  });

  it("propagates quick create fields, scopes and submit", async () => {
    const user = userEvent.setup();
    const onQuickCreate = vi.fn().mockResolvedValue(undefined);

    render(<QuickCreateHarness onQuickCreate={onQuickCreate} />);

    await user.type(screen.getByLabelText("Tenant display name"), "Acme Operations");
    await user.type(screen.getByLabelText("Application display name"), "Project Manager");
    await user.type(screen.getByLabelText("API client display name"), "Backend API");
    await user.click(screen.getByLabelText(/devices:write/i));
    await user.click(screen.getByRole("button", { name: "Quick create" }));

    expect((screen.getByLabelText("Tenant display name") as HTMLInputElement).value).toBe("Acme Operations");
    expect((screen.getByLabelText("Application display name") as HTMLInputElement).value).toBe("Project Manager");
    expect((screen.getByLabelText("API client display name") as HTMLInputElement).value).toBe("Backend API");
    expect((screen.getByLabelText(/devices:write/i) as HTMLInputElement).checked).toBe(true);
    expect(onQuickCreate).toHaveBeenCalledOnce();
  });

  it("renders and discards quick-create one-time secret", async () => {
    const user = userEvent.setup();
    const onDiscardSecret = vi.fn();

    render(
      <TenantQuickCreatePanel
        tenantDisplayName=""
        applicationDisplayName=""
        integrationClientDisplayName=""
        allowedScopes={["challenges:read"]}
        oneTimeSecret={{
          tenantId: "tenant-id",
          applicationClientId: "application-id",
          clientId: "generated-client",
          clientSecret: "one-time-client-secret",
        }}
        pending={null}
        canWrite
        onTenantDisplayNameChange={vi.fn()}
        onApplicationDisplayNameChange={vi.fn()}
        onIntegrationClientDisplayNameChange={vi.fn()}
        onToggleScope={vi.fn()}
        onQuickCreate={vi.fn()}
        onCopySecret={vi.fn()}
        onDiscardSecret={onDiscardSecret}
        onReset={vi.fn()}
      />,
    );

    expect(screen.getByText("one-time-client-secret")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Discard secret" }));

    expect(onDiscardSecret).toHaveBeenCalledOnce();
  });

  it("propagates manual create fields and status", async () => {
    const user = userEvent.setup();
    const onCreate = vi.fn().mockResolvedValue(undefined);

    render(<ManualCreateHarness onCreate={onCreate} />);

    await user.type(screen.getByLabelText("Tenant ID"), "30303030-3030-3030-3030-303030303030");
    await user.type(screen.getByLabelText("Display name"), "Migration Tenant");
    await user.type(screen.getByLabelText("Slug"), "migration-tenant");
    await user.selectOptions(screen.getByLabelText("Status"), "test");
    await user.click(screen.getByRole("button", { name: "Create tenant" }));

    expect((screen.getByLabelText("Tenant ID") as HTMLInputElement).value).toBe("30303030-3030-3030-3030-303030303030");
    expect((screen.getByLabelText("Display name") as HTMLInputElement).value).toBe("Migration Tenant");
    expect((screen.getByLabelText("Slug") as HTMLInputElement).value).toBe("migration-tenant");
    expect((screen.getByLabelText("Status") as HTMLSelectElement).value).toBe("test");
    expect(onCreate).toHaveBeenCalledOnce();
  });
});
