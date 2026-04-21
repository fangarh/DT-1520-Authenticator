import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import type { WebhookEventType } from "../../shared/types/admin-contracts";
import { webhookEventOptions } from "../../shared/types/webhook-events";
import styles from "./WebhookSubscriptionEditorPanel.module.css";

interface WebhookSubscriptionEditorPanelProps {
  applicationClientId: string;
  endpointUrl: string;
  eventTypes: WebhookEventType[];
  isActive: boolean;
  pending: boolean;
  canWrite: boolean;
  onApplicationClientIdChange: (value: string) => void;
  onEndpointUrlChange: (value: string) => void;
  onToggleEventType: (eventType: WebhookEventType) => void;
  onIsActiveChange: (value: boolean) => void;
  onSubmit: () => Promise<void>;
  onReset: () => void;
}

export function WebhookSubscriptionEditorPanel(props: WebhookSubscriptionEditorPanelProps) {
  return (
    <Panel
      eyebrow="Webhook Editor"
      title="Save subscription"
      aside={<span className={styles.scope}>{props.canWrite ? "write enabled" : "write missing"}</span>}
    >
      <div className={styles.form}>
        <label className={styles.field}>
          <span>Application Client ID</span>
          <input
            value={props.applicationClientId}
            onChange={(event) => props.onApplicationClientIdChange(event.target.value)}
            placeholder="Optional when tenant has exactly one active client"
          />
        </label>

        <label className={styles.field}>
          <span>Webhook endpoint URL</span>
          <input
            value={props.endpointUrl}
            onChange={(event) => props.onEndpointUrlChange(event.target.value)}
            placeholder="https://crm.example.com/webhooks/platform"
          />
        </label>

        <label className={styles.toggle}>
          <input
            type="checkbox"
            checked={props.isActive}
            onChange={(event) => props.onIsActiveChange(event.target.checked)}
          />
          <span>Subscription active</span>
        </label>

        <div className={styles.eventGroup}>
          <span className={styles.eventLabel}>Platform events</span>
          <div className={styles.eventList}>
            {webhookEventOptions.map((eventOption) => (
              <label key={eventOption.value} className={styles.eventItem}>
                <input
                  type="checkbox"
                  checked={props.eventTypes.includes(eventOption.value)}
                  onChange={() => props.onToggleEventType(eventOption.value)}
                />
                <span>{eventOption.label}</span>
                <small>{eventOption.description}</small>
              </label>
            ))}
          </div>
        </div>

        <p className={styles.hint}>
          Endpoint validation остается fail-closed: только `HTTPS`, без `localhost` и private-network IP literals.
        </p>

        <div className={styles.actions}>
          <Button onClick={() => void props.onSubmit()} disabled={props.pending || !props.canWrite} stretch>
            {props.pending ? "Saving..." : "Save subscription"}
          </Button>
          <Button kind="secondary" onClick={props.onReset} disabled={props.pending}>
            Clear editor
          </Button>
        </div>
      </div>
    </Panel>
  );
}
