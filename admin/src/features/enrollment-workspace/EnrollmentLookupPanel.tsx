import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import styles from "./EnrollmentLookupPanel.module.css";

interface EnrollmentLookupPanelProps {
  tenantId: string;
  externalUserId: string;
  pending: boolean;
  onTenantIdChange: (value: string) => void;
  onExternalUserIdChange: (value: string) => void;
  onSubmit: () => Promise<void>;
}

export function EnrollmentLookupPanel(props: EnrollmentLookupPanelProps) {
  return (
    <Panel eyebrow="Lookup" title="Current enrollment by user">
      <div className={styles.form}>
        <label className={styles.field}>
          <span>Tenant ID</span>
          <input value={props.tenantId} onChange={(event) => props.onTenantIdChange(event.target.value)} />
        </label>

        <label className={styles.field}>
          <span>External User ID</span>
          <input value={props.externalUserId} onChange={(event) => props.onExternalUserIdChange(event.target.value)} />
        </label>

        <Button onClick={props.onSubmit} disabled={props.pending}>
          {props.pending ? "Loading..." : "Load current"}
        </Button>
      </div>
    </Panel>
  );
}
