import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import styles from "./StartEnrollmentForm.module.css";

interface StartEnrollmentFormProps {
  applicationClientId: string;
  issuer: string;
  label: string;
  pending: boolean;
  onApplicationClientIdChange: (value: string) => void;
  onIssuerChange: (value: string) => void;
  onLabelChange: (value: string) => void;
  onSubmit: () => Promise<void>;
}

export function StartEnrollmentForm(props: StartEnrollmentFormProps) {
  return (
    <Panel eyebrow="Start" title="Create provisioning artifact">
      <div className={styles.form}>
        <label className={styles.field}>
          <span>Application Client ID</span>
          <input value={props.applicationClientId} onChange={(event) => props.onApplicationClientIdChange(event.target.value)} />
        </label>

        <label className={styles.field}>
          <span>Issuer</span>
          <input value={props.issuer} onChange={(event) => props.onIssuerChange(event.target.value)} />
        </label>

        <label className={styles.field}>
          <span>Label</span>
          <input value={props.label} onChange={(event) => props.onLabelChange(event.target.value)} />
        </label>

        <Button onClick={props.onSubmit} disabled={props.pending}>
          {props.pending ? "Starting..." : "Start enrollment"}
        </Button>
      </div>
    </Panel>
  );
}
