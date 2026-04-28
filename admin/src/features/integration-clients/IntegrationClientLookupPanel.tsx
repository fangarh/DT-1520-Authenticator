import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import styles from "./IntegrationClientLookupPanel.module.css";

interface IntegrationClientLookupPanelProps {
  tenantId: string;
  pending: boolean;
  canRead: boolean;
  onTenantIdChange: (value: string) => void;
  onSubmit: () => Promise<void>;
}

export function IntegrationClientLookupPanel(props: IntegrationClientLookupPanelProps) {
  return (
    <Panel
      eyebrow="Integration Clients"
      title="Load clients"
      aside={<span className={styles.scope}>{props.canRead ? "read enabled" : "read missing"}</span>}
    >
      <div className={styles.form}>
        <label className={styles.field}>
          <span>Tenant ID</span>
          <input
            value={props.tenantId}
            onChange={(event) => props.onTenantIdChange(event.target.value)}
            placeholder="6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"
          />
        </label>

        <Button onClick={() => void props.onSubmit()} disabled={props.pending || !props.canRead} stretch>
          {props.pending ? "Loading..." : "Load clients"}
        </Button>
      </div>
    </Panel>
  );
}
