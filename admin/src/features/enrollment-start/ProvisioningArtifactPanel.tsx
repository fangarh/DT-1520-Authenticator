import type { ProvisioningArtifact } from "../enrollment-workspace/model/provisioning-artifact";
import { Panel } from "../../shared/ui/Panel";
import styles from "./ProvisioningArtifactPanel.module.css";

interface ProvisioningArtifactPanelProps {
  artifact: ProvisioningArtifact | null;
}

export function ProvisioningArtifactPanel({ artifact }: ProvisioningArtifactPanelProps) {
  return (
    <Panel eyebrow="Artifact" title="One-time provisioning material">
      {artifact ? (
        <div className={styles.stack}>
          <div className={styles.metricRow}>
            <div>
              <span>Manual secret</span>
              <strong>{artifact.secret}</strong>
            </div>
            <div>
              <span>Label</span>
              <strong>{artifact.label}</strong>
            </div>
          </div>

          <div className={styles.metricRow}>
            <div>
              <span>Issuer</span>
              <strong>{artifact.issuer}</strong>
            </div>
            <div>
              <span>OTP profile</span>
              <strong>{artifact.algorithm} / {artifact.digits} digits / {artifact.period}s</strong>
            </div>
          </div>

          <label className={styles.block}>
            <span>otpauth URI</span>
            <textarea readOnly value={artifact.secretUri} />
          </label>
        </div>
      ) : (
        <p className={styles.empty}>Provisioning secret появится только после `start` или `replace`.</p>
      )}
    </Panel>
  );
}
