import { Notice } from "../../shared/ui/Notice";
import type { AdminSession } from "../../shared/types/admin-contracts";
import { UserDeviceDetailPanel } from "./UserDeviceDetailPanel";
import { UserDeviceListPanel } from "./UserDeviceListPanel";
import { UserDeviceLookupPanel } from "./UserDeviceLookupPanel";
import { useUserDeviceWorkspace } from "./model/useUserDeviceWorkspace";
import styles from "./UserDeviceWorkspace.module.css";

interface UserDeviceWorkspaceProps {
  session: AdminSession;
}

export function UserDeviceWorkspace({ session }: UserDeviceWorkspaceProps) {
  const workspace = useUserDeviceWorkspace(session);

  return (
    <section className={styles.layout}>
      <div className={styles.primaryColumn}>
        {workspace.notice ? <Notice {...workspace.notice} /> : null}

        <UserDeviceLookupPanel
          tenantId={workspace.lookupDraft.tenantId}
          externalUserId={workspace.lookupDraft.externalUserId}
          pending={workspace.pendingAction === "load"}
          canRead={workspace.canRead}
          onTenantIdChange={(tenantId) => workspace.setLookupDraft((current) => ({ ...current, tenantId }))}
          onExternalUserIdChange={(externalUserId) => workspace.setLookupDraft((current) => ({ ...current, externalUserId }))}
          onSubmit={workspace.loadDevices}
        />

        <UserDeviceListPanel
          devices={workspace.devices}
          selectedDeviceId={workspace.selectedDeviceId}
          onSelect={workspace.selectDevice}
        />
      </div>

      <UserDeviceDetailPanel
        device={workspace.selectedDevice}
        loadedScope={workspace.loadedScope}
        hasDraftScopeChanges={workspace.hasDraftScopeChanges}
        pending={workspace.pendingAction === "revoke"}
        canWrite={workspace.canWrite}
        revokeArmed={workspace.revokeArmed}
        onRevokeArmedChange={workspace.setRevokeArmed}
        onRevoke={workspace.revokeDevice}
      />
    </section>
  );
}
