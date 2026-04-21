import type { AdminUserDeviceView } from "../../shared/types/admin-contracts";
import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { UserDeviceStatusBadge } from "./UserDeviceStatusBadge";
import styles from "./UserDeviceDetailPanel.module.css";

interface LoadedScope {
  tenantId: string;
  externalUserId: string;
}

interface UserDeviceDetailPanelProps {
  device: AdminUserDeviceView | null;
  loadedScope: LoadedScope | null;
  hasDraftScopeChanges: boolean;
  pending: boolean;
  canWrite: boolean;
  revokeArmed: boolean;
  onRevokeArmedChange: (armed: boolean) => void;
  onRevoke: () => Promise<void>;
}

export function UserDeviceDetailPanel(props: UserDeviceDetailPanelProps) {
  const isActive = props.device?.status === "active";

  return (
    <Panel
      eyebrow="Device Detail"
      title="Selected device"
      aside={props.device ? <UserDeviceStatusBadge status={props.device.status} /> : undefined}
    >
      {!props.device ? (
        <p className={styles.empty}>Выберите устройство из списка, чтобы увидеть lifecycle metadata и revoke controls.</p>
      ) : (
        <div className={styles.layout}>
          <div className={styles.summary}>
            <strong className={styles.deviceId}>{props.device.deviceId}</strong>
            <div className={styles.flagRow}>
              <span className={styles.platform}>{props.device.platform}</span>
              {props.device.isPushCapable ? <span className={styles.pushCapable}>push capable</span> : null}
            </div>
            {props.loadedScope ? (
              <p className={styles.scope}>
                Loaded scope: <strong>{props.loadedScope.tenantId}</strong> / <strong>{props.loadedScope.externalUserId}</strong>
              </p>
            ) : null}
            {props.hasDraftScopeChanges ? (
              <p className={styles.scopeWarning}>
                Draft lookup changed after load. Revoke action stays bound to the last loaded scope until you reload devices.
              </p>
            ) : null}
          </div>

          <dl className={styles.meta}>
            <div>
              <dt>Activated</dt>
              <dd>{formatUtcInstant(props.device.activatedAtUtc)}</dd>
            </div>
            <div>
              <dt>Last seen</dt>
              <dd>{formatUtcInstant(props.device.lastSeenAtUtc)}</dd>
            </div>
            <div>
              <dt>Revoked at</dt>
              <dd>{formatUtcInstant(props.device.revokedAtUtc)}</dd>
            </div>
            <div>
              <dt>Blocked at</dt>
              <dd>{formatUtcInstant(props.device.blockedAtUtc)}</dd>
            </div>
          </dl>

          <div className={styles.actionCard}>
            <strong>Revoke device</strong>
            <p className={styles.actionCopy}>
              Revoke invalidates the current device session and keeps only sanitized lifecycle history for operator review.
            </p>

            {!props.canWrite ? (
              <p className={styles.actionHint}>Permission `devices.write` is required for destructive actions.</p>
            ) : null}
            {props.canWrite && !isActive ? (
              <p className={styles.actionHint}>Only devices in `active` state can be revoked from this workspace.</p>
            ) : null}

            <label className={styles.confirmation}>
              <input
                type="checkbox"
                checked={props.revokeArmed}
                disabled={!props.canWrite || !isActive || props.pending}
                onChange={(event) => props.onRevokeArmedChange(event.target.checked)}
              />
              <span>I understand that revoke will immediately invalidate this device session.</span>
            </label>

            <Button
              kind="danger"
              stretch
              disabled={!props.canWrite || !isActive || !props.revokeArmed || props.pending}
              onClick={() => void props.onRevoke()}
            >
              {props.pending ? "Revoking..." : "Revoke device"}
            </Button>
          </div>
        </div>
      )}
    </Panel>
  );
}
