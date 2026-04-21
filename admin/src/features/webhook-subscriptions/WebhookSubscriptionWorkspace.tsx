import { Notice } from "../../shared/ui/Notice";
import type { AdminSession } from "../../shared/types/admin-contracts";
import { WebhookSubscriptionEditorPanel } from "./WebhookSubscriptionEditorPanel";
import { WebhookSubscriptionListPanel } from "./WebhookSubscriptionListPanel";
import { WebhookSubscriptionLookupPanel } from "./WebhookSubscriptionLookupPanel";
import { useWebhookSubscriptionWorkspace } from "./model/useWebhookSubscriptionWorkspace";
import styles from "./WebhookSubscriptionWorkspace.module.css";

interface WebhookSubscriptionWorkspaceProps {
  session: AdminSession;
}

export function WebhookSubscriptionWorkspace({ session }: WebhookSubscriptionWorkspaceProps) {
  const workspace = useWebhookSubscriptionWorkspace(session);

  return (
    <section className={styles.layout}>
      <div className={styles.primaryColumn}>
        {workspace.notice ? <Notice {...workspace.notice} /> : null}

        <WebhookSubscriptionLookupPanel
          tenantId={workspace.lookupDraft.tenantId}
          applicationClientId={workspace.lookupDraft.applicationClientId}
          pending={workspace.pendingAction === "load"}
          canRead={workspace.canRead}
          onTenantIdChange={(tenantId) => workspace.setLookupDraft((current) => ({ ...current, tenantId }))}
          onApplicationClientIdChange={(applicationClientId) => workspace.setLookupDraft((current) => ({ ...current, applicationClientId }))}
          onSubmit={workspace.loadSubscriptions}
        />

        <WebhookSubscriptionListPanel
          subscriptions={workspace.subscriptions}
          selectedSubscriptionId={workspace.selectedSubscriptionId}
          onSelect={workspace.selectSubscription}
        />
      </div>

      <WebhookSubscriptionEditorPanel
        applicationClientId={workspace.editorDraft.applicationClientId}
        endpointUrl={workspace.editorDraft.endpointUrl}
        eventTypes={workspace.editorDraft.eventTypes}
        isActive={workspace.editorDraft.isActive}
        pending={workspace.pendingAction === "save"}
        canWrite={workspace.canWrite}
        onApplicationClientIdChange={(applicationClientId) => workspace.setEditorDraft((current) => ({ ...current, applicationClientId }))}
        onEndpointUrlChange={(endpointUrl) => workspace.setEditorDraft((current) => ({ ...current, endpointUrl }))}
        onToggleEventType={workspace.toggleEventType}
        onIsActiveChange={(isActive) => workspace.setEditorDraft((current) => ({ ...current, isActive }))}
        onSubmit={workspace.saveSubscription}
        onReset={workspace.resetEditor}
      />
    </section>
  );
}
