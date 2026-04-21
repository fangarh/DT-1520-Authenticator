import { Notice } from "../../shared/ui/Notice";
import type { AdminSession } from "../../shared/types/admin-contracts";
import { DeliveryStatusDetailPanel } from "./DeliveryStatusDetailPanel";
import { DeliveryStatusListPanel } from "./DeliveryStatusListPanel";
import { DeliveryStatusLookupPanel } from "./DeliveryStatusLookupPanel";
import { useDeliveryStatusWorkspace } from "./model/useDeliveryStatusWorkspace";
import styles from "./DeliveryStatusWorkspace.module.css";

interface DeliveryStatusWorkspaceProps {
  session: AdminSession;
}

export function DeliveryStatusWorkspace({ session }: DeliveryStatusWorkspaceProps) {
  const workspace = useDeliveryStatusWorkspace(session);

  return (
    <section className={styles.layout}>
      <div className={styles.primaryColumn}>
        {workspace.notice ? <Notice {...workspace.notice} /> : null}

        <DeliveryStatusLookupPanel
          tenantId={workspace.lookupDraft.tenantId}
          applicationClientId={workspace.lookupDraft.applicationClientId}
          channel={workspace.lookupDraft.channel}
          status={workspace.lookupDraft.status}
          limit={workspace.lookupDraft.limit}
          pending={workspace.pendingAction === "load"}
          canRead={workspace.canRead}
          onTenantIdChange={(tenantId) => workspace.setLookupDraft((current) => ({ ...current, tenantId }))}
          onApplicationClientIdChange={(applicationClientId) => workspace.setLookupDraft((current) => ({ ...current, applicationClientId }))}
          onChannelChange={(channel) => workspace.setLookupDraft((current) => ({ ...current, channel: channel as typeof current.channel }))}
          onStatusChange={(status) => workspace.setLookupDraft((current) => ({ ...current, status: status as typeof current.status }))}
          onLimitChange={(limit) => workspace.setLookupDraft((current) => ({ ...current, limit }))}
          onSubmit={workspace.loadDeliveries}
        />

        <DeliveryStatusListPanel
          deliveries={workspace.deliveries}
          selectedDeliveryId={workspace.selectedDeliveryId}
          onSelect={workspace.selectDelivery}
        />
      </div>

      <DeliveryStatusDetailPanel delivery={workspace.selectedDelivery} />
    </section>
  );
}
