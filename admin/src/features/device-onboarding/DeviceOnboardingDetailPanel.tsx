import type { AdminDeviceOnboardingArtifactView } from "../../shared/types/admin-contracts";
import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { DeviceOnboardingStatusBadge } from "./DeviceOnboardingStatusBadge";
import styles from "./DeviceOnboardingDetailPanel.module.css";

interface DeviceOnboardingDetailPanelProps {
  artifact: AdminDeviceOnboardingArtifactView | null;
  pending: boolean;
  canWrite: boolean;
  revokeArmed: boolean;
  onRevokeArmedChange: (armed: boolean) => void;
  onRevoke: () => Promise<void>;
}

export function DeviceOnboardingDetailPanel(props: DeviceOnboardingDetailPanelProps) {
  const canRevoke = props.artifact?.status === "pending";

  return (
    <Panel
      eyebrow="QR Artifact Detail"
      title="Sanitized metadata"
      aside={props.artifact ? <DeviceOnboardingStatusBadge status={props.artifact.status} /> : undefined}
    >
      {!props.artifact ? (
        <p className={styles.empty}>Выберите QR artifact из списка, чтобы увидеть status, binding и revoke controls.</p>
      ) : (
        <div className={styles.layout}>
          <div className={styles.summary}>
            <strong>{props.artifact.activationCodeId}</strong>
            <p>{props.artifact.tenantId}</p>
          </div>

          <dl className={styles.meta}>
            <div>
              <dt>Application Client</dt>
              <dd>{props.artifact.applicationClientId}</dd>
            </div>
            <div>
              <dt>External User</dt>
              <dd>{props.artifact.externalUserId}</dd>
            </div>
            <div>
              <dt>Platform</dt>
              <dd>{props.artifact.platform}</dd>
            </div>
            <div>
              <dt>Created</dt>
              <dd>{formatUtcInstant(props.artifact.createdAtUtc)}</dd>
            </div>
            <div>
              <dt>Expires</dt>
              <dd>{formatUtcInstant(props.artifact.expiresAtUtc)}</dd>
            </div>
            <div>
              <dt>Consumed</dt>
              <dd>{formatUtcInstant(props.artifact.consumedAtUtc)}</dd>
            </div>
            <div>
              <dt>Revoked</dt>
              <dd>{formatUtcInstant(props.artifact.revokedAtUtc)}</dd>
            </div>
          </dl>

          <div className={styles.actionCard}>
            <strong>Revoke QR artifact</strong>
            <p className={styles.actionCopy}>
              Revoke invalidates an unused artifact without exposing activation payload or hash in operator read paths.
            </p>

            {!props.canWrite ? (
              <p className={styles.actionHint}>Permission `devices.write` is required for revoke.</p>
            ) : null}
            {props.canWrite && !canRevoke ? (
              <p className={styles.actionHint}>Only `pending` QR artifacts can be revoked from this workspace.</p>
            ) : null}

            <label className={styles.confirmation}>
              <input
                type="checkbox"
                checked={props.revokeArmed}
                disabled={!props.canWrite || !canRevoke || props.pending}
                onChange={(event) => props.onRevokeArmedChange(event.target.checked)}
              />
              <span>I understand that revoke will prevent this QR from activating a device.</span>
            </label>

            <Button
              kind="danger"
              stretch
              disabled={!props.canWrite || !canRevoke || !props.revokeArmed || props.pending}
              onClick={() => void props.onRevoke()}
            >
              {props.pending ? "Revoking..." : "Revoke QR"}
            </Button>
          </div>
        </div>
      )}
    </Panel>
  );
}
