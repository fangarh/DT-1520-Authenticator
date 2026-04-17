import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import styles from "./RevokeEnrollmentPanel.module.css";

interface RevokeEnrollmentPanelProps {
  disabled: boolean;
  pending: boolean;
  onSubmit: () => Promise<void>;
}

export function RevokeEnrollmentPanel(props: RevokeEnrollmentPanelProps) {
  return (
    <Panel eyebrow="Revoke" title="Destructive operator action">
      <div className={styles.stack}>
        <p>
          Revoke закрывает текущий enrollment и очищает pending replacement
          material. Старый provisioning artifact после этого больше не должен
          использоваться.
        </p>
        <Button kind="danger" onClick={props.onSubmit} disabled={props.disabled || props.pending}>
          {props.pending ? "Revoking..." : "Revoke enrollment"}
        </Button>
      </div>
    </Panel>
  );
}
