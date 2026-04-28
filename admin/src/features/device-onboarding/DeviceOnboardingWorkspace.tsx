import { Notice } from "../../shared/ui/Notice";
import type { AdminSession } from "../../shared/types/admin-contracts";
import { DeviceOnboardingCreatePanel } from "./DeviceOnboardingCreatePanel";
import { DeviceOnboardingDetailPanel } from "./DeviceOnboardingDetailPanel";
import { DeviceOnboardingListPanel } from "./DeviceOnboardingListPanel";
import { DeviceOnboardingLookupPanel } from "./DeviceOnboardingLookupPanel";
import { DeviceOnboardingQrPanel } from "./DeviceOnboardingQrPanel";
import { useDeviceOnboardingWorkspace } from "./model/useDeviceOnboardingWorkspace";
import styles from "./DeviceOnboardingWorkspace.module.css";

interface DeviceOnboardingWorkspaceProps {
  session: AdminSession;
}

export function DeviceOnboardingWorkspace({ session }: DeviceOnboardingWorkspaceProps) {
  const workspace = useDeviceOnboardingWorkspace(session);

  return (
    <section className={styles.layout}>
      <div className={styles.primaryColumn}>
        {workspace.notice ? <Notice {...workspace.notice} /> : null}

        <DeviceOnboardingLookupPanel
          tenantId={workspace.lookupDraft.tenantId}
          externalUserId={workspace.lookupDraft.externalUserId}
          applicationClientId={workspace.lookupDraft.applicationClientId}
          status={workspace.lookupDraft.status}
          limit={workspace.lookupDraft.limit}
          pending={workspace.pendingAction === "load"}
          canRead={workspace.canRead}
          onTenantIdChange={(tenantId) => workspace.setLookupDraft((current) => ({ ...current, tenantId }))}
          onExternalUserIdChange={(externalUserId) => workspace.setLookupDraft((current) => ({ ...current, externalUserId }))}
          onApplicationClientIdChange={(applicationClientId) => workspace.setLookupDraft((current) => ({ ...current, applicationClientId }))}
          onStatusChange={(status) => workspace.setLookupDraft((current) => ({ ...current, status }))}
          onLimitChange={(limit) => workspace.setLookupDraft((current) => ({ ...current, limit }))}
          onSubmit={workspace.loadArtifacts}
        />

        <DeviceOnboardingListPanel
          artifacts={workspace.artifacts}
          selectedArtifactId={workspace.selectedArtifactId}
          onSelect={workspace.selectArtifact}
        />
      </div>

      <div className={styles.secondaryColumn}>
        <DeviceOnboardingCreatePanel
          tenantId={workspace.createDraft.tenantId}
          applicationClientId={workspace.createDraft.applicationClientId}
          externalUserId={workspace.createDraft.externalUserId}
          platform={workspace.createDraft.platform}
          ttlMinutes={workspace.createDraft.ttlMinutes}
          pending={workspace.pendingAction}
          canWrite={workspace.canWrite}
          onTenantIdChange={(tenantId) => workspace.setCreateDraft((current) => ({ ...current, tenantId }))}
          onApplicationClientIdChange={(applicationClientId) => workspace.setCreateDraft((current) => ({ ...current, applicationClientId }))}
          onExternalUserIdChange={(externalUserId) => workspace.setCreateDraft((current) => ({ ...current, externalUserId }))}
          onPlatformChange={(platform) => workspace.setCreateDraft((current) => ({ ...current, platform }))}
          onTtlMinutesChange={(ttlMinutes) => workspace.setCreateDraft((current) => ({ ...current, ttlMinutes }))}
          onCreate={workspace.createArtifact}
          onReset={workspace.resetCreateDraft}
        />

        <DeviceOnboardingQrPanel
          payload={workspace.oneTimePayload}
          pending={workspace.pendingAction}
          onCopy={workspace.copyActivationPayload}
          onDiscard={workspace.discardActivationPayload}
        />

        <DeviceOnboardingDetailPanel
          artifact={workspace.selectedArtifact}
          pending={workspace.pendingAction === "revoke"}
          canWrite={workspace.canWrite}
          revokeArmed={workspace.revokeArmed}
          onRevokeArmedChange={workspace.setRevokeArmed}
          onRevoke={workspace.revokeArtifact}
        />
      </div>
    </section>
  );
}
