import type { AdminUserDeviceView } from "../../shared/types/admin-contracts";
import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { UserDeviceStatusBadge } from "./UserDeviceStatusBadge";
import styles from "./UserDeviceListPanel.module.css";

interface UserDeviceListPanelProps {
  devices: AdminUserDeviceView[];
  selectedDeviceId: string | null;
  onSelect: (device: AdminUserDeviceView) => void;
}

export function UserDeviceListPanel(props: UserDeviceListPanelProps) {
  return (
    <Panel eyebrow="Device Inventory" title="Current and recent devices">
      {props.devices.length === 0 ? (
        <p className={styles.empty}>Сначала загрузите tenant и user scope, чтобы увидеть current/recent device history.</p>
      ) : (
        <div className={styles.list}>
          {props.devices.map((device) => {
            const isSelected = device.deviceId === props.selectedDeviceId;
            return (
              <article
                key={device.deviceId}
                className={[styles.card, isSelected ? styles.selected : ""].join(" ")}
              >
                <div className={styles.cardHeader}>
                  <div className={styles.headerSummary}>
                    <UserDeviceStatusBadge status={device.status} />
                    <span className={styles.platform}>{device.platform}</span>
                    {device.isPushCapable ? <span className={styles.pushCapable}>push capable</span> : null}
                  </div>
                  <Button kind={isSelected ? "primary" : "secondary"} onClick={() => props.onSelect(device)}>
                    {isSelected ? "Selected" : "Inspect"}
                  </Button>
                </div>

                <strong className={styles.deviceId}>{device.deviceId}</strong>

                <dl className={styles.meta}>
                  <div>
                    <dt>Activated</dt>
                    <dd>{formatUtcInstant(device.activatedAtUtc)}</dd>
                  </div>
                  <div>
                    <dt>Last seen</dt>
                    <dd>{formatUtcInstant(device.lastSeenAtUtc)}</dd>
                  </div>
                </dl>
              </article>
            );
          })}
        </div>
      )}
    </Panel>
  );
}
