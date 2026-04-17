import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import styles from "./ConfirmEnrollmentForm.module.css";

interface ConfirmEnrollmentFormProps {
  code: string;
  disabled: boolean;
  pending: boolean;
  onCodeChange: (value: string) => void;
  onSubmit: () => Promise<void>;
}

export function ConfirmEnrollmentForm(props: ConfirmEnrollmentFormProps) {
  return (
    <Panel eyebrow="Confirm" title="Confirm enrollment or replacement">
      <div className={styles.form}>
        <label className={styles.field}>
          <span>Authenticator code</span>
          <input
            value={props.code}
            maxLength={6}
            inputMode="numeric"
            onChange={(event) => props.onCodeChange(event.target.value)}
            placeholder="123456"
          />
        </label>

        <Button onClick={props.onSubmit} disabled={props.disabled || props.pending}>
          {props.pending ? "Confirming..." : "Confirm"}
        </Button>
      </div>
    </Panel>
  );
}
