import { Notice } from "../../shared/ui/Notice";
import type { AdminSession } from "../../shared/types/admin-contracts";
import { IntegrationClientCreatePanel } from "./IntegrationClientCreatePanel";
import { IntegrationClientDetailPanel } from "./IntegrationClientDetailPanel";
import { IntegrationClientLifecyclePanel } from "./IntegrationClientLifecyclePanel";
import { IntegrationClientListPanel } from "./IntegrationClientListPanel";
import { IntegrationClientLookupPanel } from "./IntegrationClientLookupPanel";
import { useIntegrationClientWorkspace } from "./model/useIntegrationClientWorkspace";
import styles from "./IntegrationClientWorkspace.module.css";

interface IntegrationClientWorkspaceProps {
  session: AdminSession;
}

export function IntegrationClientWorkspace({ session }: IntegrationClientWorkspaceProps) {
  const workspace = useIntegrationClientWorkspace(session);

  return (
    <section className={styles.layout}>
      <div className={styles.primaryColumn}>
        {workspace.notice ? <Notice {...workspace.notice} /> : null}

        <IntegrationClientLookupPanel
          tenantId={workspace.lookupDraft.tenantId}
          pending={workspace.pendingAction === "load"}
          canRead={workspace.canRead}
          onTenantIdChange={(tenantId) => workspace.setLookupDraft({ tenantId })}
          onSubmit={workspace.loadClients}
        />

        <IntegrationClientListPanel
          clients={workspace.clients}
          selectedClientId={workspace.selectedClientId}
          onSelect={workspace.selectClient}
        />

        <IntegrationClientDetailPanel client={workspace.selectedClient} />
      </div>

      <div className={styles.secondaryColumn}>
        <IntegrationClientCreatePanel
          tenantId={workspace.createDraft.tenantId}
          applicationClientId={workspace.createDraft.applicationClientId}
          clientId={workspace.createDraft.clientId}
          allowedScopes={workspace.createDraft.allowedScopes}
          oneTimeSecret={workspace.oneTimeSecret}
          pending={workspace.pendingAction}
          canWrite={workspace.canWrite}
          onTenantIdChange={(tenantId) => workspace.setCreateDraft((current) => ({ ...current, tenantId }))}
          onApplicationClientIdChange={(applicationClientId) => workspace.setCreateDraft((current) => ({ ...current, applicationClientId }))}
          onClientIdChange={(clientId) => workspace.setCreateDraft((current) => ({ ...current, clientId }))}
          onToggleScope={workspace.toggleCreateScope}
          onCreate={workspace.createClient}
          onCopySecret={workspace.copyCreateSecret}
          onDiscardSecret={workspace.discardCreateSecret}
          onReset={workspace.resetCreateDraft}
        />

        <IntegrationClientLifecyclePanel
          client={workspace.selectedClient}
          scopeDraft={workspace.scopeDraft}
          hasScopeChanges={workspace.hasScopeChanges}
          rotatedSecret={workspace.rotatedSecret}
          pending={workspace.pendingAction}
          canWrite={workspace.canWrite}
          rotateArmed={workspace.rotateArmed}
          deactivateArmed={workspace.deactivateArmed}
          reactivateArmed={workspace.reactivateArmed}
          onToggleScope={workspace.toggleLifecycleScope}
          onUpdateScopes={workspace.updateScopes}
          onRotateSecret={workspace.rotateSecret}
          onDeactivate={workspace.deactivateClient}
          onReactivate={workspace.reactivateClient}
          onCopyRotatedSecret={workspace.copyRotatedSecret}
          onDiscardRotatedSecret={workspace.discardRotatedSecret}
          onRotateArmedChange={workspace.setRotateArmed}
          onDeactivateArmedChange={workspace.setDeactivateArmed}
          onReactivateArmedChange={workspace.setReactivateArmed}
        />
      </div>
    </section>
  );
}
