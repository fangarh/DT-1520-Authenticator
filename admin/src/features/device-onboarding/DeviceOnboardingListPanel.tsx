import type { AdminDeviceOnboardingArtifactView } from "../../shared/types/admin-contracts";
import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { DeviceOnboardingStatusBadge } from "./DeviceOnboardingStatusBadge";
import styles from "./DeviceOnboardingListPanel.module.css";

interface DeviceOnboardingListPanelProps {
  artifacts: AdminDeviceOnboardingArtifactView[];
  selectedArtifactId: string | null;
  onSelect: (artifact: AdminDeviceOnboardingArtifactView) => void;
}

export function DeviceOnboardingListPanel(props: DeviceOnboardingListPanelProps) {
  return (
    <Panel eyebrow="QR Artifact Inventory" title="Recent artifacts">
      {props.artifacts.length === 0 ? (
        <p className={styles.empty}>Сначала загрузите tenant scope или создайте новый QR artifact.</p>
      ) : (
        <div className={styles.list}>
          {props.artifacts.map((artifact) => {
            const isSelected = artifact.activationCodeId === props.selectedArtifactId;
            return (
              <article key={artifact.activationCodeId} className={[styles.card, isSelected ? styles.selected : ""].join(" ")}>
                <div className={styles.cardHeader}>
                  <div className={styles.titleBlock}>
                    <strong>{artifact.activationCodeId}</strong>
                    <DeviceOnboardingStatusBadge status={artifact.status} />
                  </div>
                  <Button kind={isSelected ? "primary" : "secondary"} onClick={() => props.onSelect(artifact)}>
                    {isSelected ? "Selected" : "Inspect QR"}
                  </Button>
                </div>

                <dl className={styles.meta}>
                  <div>
                    <dt>User</dt>
                    <dd>{artifact.externalUserId}</dd>
                  </div>
                  <div>
                    <dt>Expires</dt>
                    <dd>{formatUtcInstant(artifact.expiresAtUtc)}</dd>
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
