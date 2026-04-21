import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import styles from "./WebhookSubscriptionLookupPanel.module.css";

interface WebhookSubscriptionLookupPanelProps {
  tenantId: string;
  applicationClientId: string;
  pending: boolean;
  canRead: boolean;
  onTenantIdChange: (value: string) => void;
  onApplicationClientIdChange: (value: string) => void;
  onSubmit: () => Promise<void>;
}

export function WebhookSubscriptionLookupPanel(props: WebhookSubscriptionLookupPanelProps) {
  return (
    <Panel
      eyebrow="Webhook Scope"
      title="Load subscriptions"
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

        <label className={styles.field}>
          <span>Application Client Filter</span>
          <input
            value={props.applicationClientId}
            onChange={(event) => props.onApplicationClientIdChange(event.target.value)}
            placeholder="Optional GUID filter"
          />
        </label>

        <Button onClick={() => void props.onSubmit()} disabled={props.pending || !props.canRead} stretch>
          {props.pending ? "Loading..." : "Load subscriptions"}
        </Button>
      </div>
    </Panel>
  );
}
