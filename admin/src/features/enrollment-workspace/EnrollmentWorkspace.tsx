import { useDeferredValue } from "react";
import { ConfirmEnrollmentForm } from "../enrollment-confirm/ConfirmEnrollmentForm";
import { RevokeEnrollmentPanel } from "../enrollment-revoke/RevokeEnrollmentPanel";
import { ReplaceEnrollmentPanel } from "../enrollment-replace/ReplaceEnrollmentPanel";
import { ProvisioningArtifactPanel } from "../enrollment-start/ProvisioningArtifactPanel";
import { StartEnrollmentForm } from "../enrollment-start/StartEnrollmentForm";
import { EnrollmentStatusCard } from "../enrollment-status/EnrollmentStatusCard";
import { Notice } from "../../shared/ui/Notice";
import type { AdminSession } from "../../shared/types/admin-contracts";
import { EnrollmentLookupPanel } from "./EnrollmentLookupPanel";
import { useEnrollmentWorkspace } from "./model/useEnrollmentWorkspace";
import styles from "./EnrollmentWorkspace.module.css";

interface EnrollmentWorkspaceProps {
  session: AdminSession;
}

export function EnrollmentWorkspace({ session }: EnrollmentWorkspaceProps) {
  const workspace = useEnrollmentWorkspace(session);
  const deferredCurrent = useDeferredValue(workspace.current);

  return (
    <section className={styles.layout}>
      <div className={styles.primaryColumn}>
        {workspace.notice ? <Notice {...workspace.notice} /> : null}

        <EnrollmentLookupPanel
          tenantId={workspace.lookupDraft.tenantId}
          externalUserId={workspace.lookupDraft.externalUserId}
          pending={workspace.pendingAction === "lookup"}
          onTenantIdChange={(tenantId) => workspace.setLookupDraft((current) => ({ ...current, tenantId }))}
          onExternalUserIdChange={(externalUserId) => workspace.setLookupDraft((current) => ({ ...current, externalUserId }))}
          onSubmit={workspace.lookupCurrent}
        />

        <EnrollmentStatusCard enrollment={deferredCurrent} />

        <div className={styles.actionGrid}>
          <StartEnrollmentForm
            applicationClientId={workspace.startDraft.applicationClientId}
            issuer={workspace.startDraft.issuer}
            label={workspace.startDraft.label}
            pending={workspace.pendingAction === "start"}
            onApplicationClientIdChange={(applicationClientId) => workspace.setStartDraft((current) => ({ ...current, applicationClientId }))}
            onIssuerChange={(issuer) => workspace.setStartDraft((current) => ({ ...current, issuer }))}
            onLabelChange={(label) => workspace.setStartDraft((current) => ({ ...current, label }))}
            onSubmit={workspace.startEnrollment}
          />

          <ConfirmEnrollmentForm
            code={workspace.confirmCode}
            disabled={!workspace.canConfirm}
            pending={workspace.pendingAction === "confirm"}
            onCodeChange={workspace.setConfirmCode}
            onSubmit={workspace.confirmEnrollment}
          />

          <ReplaceEnrollmentPanel
            disabled={!workspace.canReplace}
            pending={workspace.pendingAction === "replace"}
            onSubmit={workspace.replaceEnrollment}
          />

          <RevokeEnrollmentPanel
            disabled={!workspace.canRevoke}
            pending={workspace.pendingAction === "revoke"}
            onSubmit={workspace.revokeEnrollment}
          />
        </div>
      </div>

      <ProvisioningArtifactPanel artifact={workspace.artifact} />
    </section>
  );
}
