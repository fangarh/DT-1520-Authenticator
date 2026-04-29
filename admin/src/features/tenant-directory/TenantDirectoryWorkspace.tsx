import { Notice } from "../../shared/ui/Notice";
import type { AdminSession } from "../../shared/types/admin-contracts";
import { TenantDirectoryDetailPanel } from "./TenantDirectoryDetailPanel";
import { TenantDirectoryListPanel } from "./TenantDirectoryListPanel";
import { TenantManualCreatePanel } from "./TenantManualCreatePanel";
import { TenantQuickCreatePanel } from "./TenantQuickCreatePanel";
import { TenantManagementWorkspace } from "../tenant-management/TenantManagementWorkspace";
import { useTenantDirectoryWorkspace } from "./model/useTenantDirectoryWorkspace";
import styles from "./TenantDirectoryWorkspace.module.css";

interface TenantDirectoryWorkspaceProps {
  session: AdminSession;
}

export function TenantDirectoryWorkspace({ session }: TenantDirectoryWorkspaceProps) {
  const workspace = useTenantDirectoryWorkspace(session);

  return (
    <section className={styles.layout}>
      <div className={styles.primaryColumn}>
        {workspace.notice ? <Notice {...workspace.notice} /> : null}

        <TenantDirectoryListPanel
          tenants={workspace.tenants}
          selectedTenantId={workspace.selectedTenantId}
          pending={workspace.pendingAction === "load"}
          canRead={workspace.canRead}
          onLoad={workspace.loadTenants}
          onSelect={workspace.selectTenant}
        />

        <TenantDirectoryDetailPanel directory={workspace.directory} />

        <TenantManagementWorkspace session={session} directory={workspace.directory} />
      </div>

      <div className={styles.secondaryColumn}>
        <TenantQuickCreatePanel
          tenantDisplayName={workspace.quickCreateDraft.tenantDisplayName}
          applicationDisplayName={workspace.quickCreateDraft.applicationDisplayName}
          integrationClientDisplayName={workspace.quickCreateDraft.integrationClientDisplayName}
          allowedScopes={workspace.quickCreateDraft.allowedScopes}
          oneTimeSecret={workspace.oneTimeSecret}
          pending={workspace.pendingAction}
          canWrite={workspace.canWrite}
          onTenantDisplayNameChange={(tenantDisplayName) => workspace.setQuickCreateDraft((current) => ({ ...current, tenantDisplayName }))}
          onApplicationDisplayNameChange={(applicationDisplayName) => workspace.setQuickCreateDraft((current) => ({ ...current, applicationDisplayName }))}
          onIntegrationClientDisplayNameChange={(integrationClientDisplayName) => workspace.setQuickCreateDraft((current) => ({ ...current, integrationClientDisplayName }))}
          onToggleScope={workspace.toggleQuickCreateScope}
          onQuickCreate={workspace.quickCreateTenant}
          onCopySecret={workspace.copySecret}
          onDiscardSecret={workspace.discardSecret}
          onReset={workspace.resetQuickCreateDraft}
        />

        <TenantManualCreatePanel
          tenantId={workspace.manualCreateDraft.tenantId}
          displayName={workspace.manualCreateDraft.displayName}
          slug={workspace.manualCreateDraft.slug}
          status={workspace.manualCreateDraft.status}
          pending={workspace.pendingAction}
          canWrite={workspace.canWrite}
          onTenantIdChange={(tenantId) => workspace.setManualCreateDraft((current) => ({ ...current, tenantId }))}
          onDisplayNameChange={(displayName) => workspace.setManualCreateDraft((current) => ({ ...current, displayName }))}
          onSlugChange={(slug) => workspace.setManualCreateDraft((current) => ({ ...current, slug }))}
          onStatusChange={(status) => workspace.setManualCreateDraft((current) => ({ ...current, status }))}
          onCreate={workspace.createTenant}
          onReset={workspace.resetManualCreateDraft}
        />
      </div>
    </section>
  );
}
