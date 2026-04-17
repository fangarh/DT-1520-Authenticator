import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import styles from "./ReplaceEnrollmentPanel.module.css";

interface ReplaceEnrollmentPanelProps {
  disabled: boolean;
  pending: boolean;
  onSubmit: () => Promise<void>;
}

export function ReplaceEnrollmentPanel(props: ReplaceEnrollmentPanelProps) {
  return (
    <Panel eyebrow="Replace" title="Start safe replacement">
      <div className={styles.stack}>
        <p>
          Replacement выдает новый provisioning artifact, но старый фактор
          остается активным до успешного confirm.
        </p>
        <Button kind="secondary" onClick={props.onSubmit} disabled={props.disabled || props.pending}>
          {props.pending ? "Starting..." : "Start replacement"}
        </Button>
      </div>
    </Panel>
  );
}
