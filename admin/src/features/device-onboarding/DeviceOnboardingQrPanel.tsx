import { QRCodeSVG } from "qrcode.react";
import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import type { DeviceOnboardingPendingAction, OneTimeActivationPayload } from "./model/deviceOnboardingWorkspaceModel";
import styles from "./DeviceOnboardingQrPanel.module.css";

interface DeviceOnboardingQrPanelProps {
  payload: OneTimeActivationPayload | null;
  pending: DeviceOnboardingPendingAction;
  onCopy: () => Promise<void>;
  onDiscard: () => void;
}

export function DeviceOnboardingQrPanel(props: DeviceOnboardingQrPanelProps) {
  return (
    <Panel eyebrow="One-Time Payload" title="QR for mobile activation">
      {!props.payload ? (
        <p className={styles.empty}>Создайте QR artifact, чтобы показать one-time activation payload в текущей browser session.</p>
      ) : (
        <div className={styles.layout} aria-live="polite">
          <div className={styles.qrFrame}>
            <QRCodeSVG
              value={props.payload.activationPayload}
              size={224}
              level="M"
              marginSize={4}
              title="One-time device activation QR"
              role="img"
              aria-label="One-time device activation QR"
            />
          </div>

          <div className={styles.payloadBlock}>
            <div>
              <strong>Artifact {props.payload.activationCodeId}</strong>
              <p>Expires: {formatUtcInstant(props.payload.expiresAtUtc)}</p>
            </div>
            <code>{props.payload.activationPayload}</code>
            <div className={styles.actions}>
              <Button kind="secondary" onClick={() => void props.onCopy()} disabled={props.pending === "copy"}>
                {props.pending === "copy" ? "Copying..." : "Copy payload"}
              </Button>
              <Button kind="danger" onClick={props.onDiscard}>
                Discard payload
              </Button>
            </div>
          </div>
        </div>
      )}
    </Panel>
  );
}
