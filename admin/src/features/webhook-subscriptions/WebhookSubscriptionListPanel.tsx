import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import type { WebhookSubscriptionView } from "../../shared/types/admin-contracts";
import styles from "./WebhookSubscriptionListPanel.module.css";

interface WebhookSubscriptionListPanelProps {
  subscriptions: WebhookSubscriptionView[];
  selectedSubscriptionId: string | null;
  onSelect: (subscription: WebhookSubscriptionView) => void;
}

export function WebhookSubscriptionListPanel(props: WebhookSubscriptionListPanelProps) {
  return (
    <Panel eyebrow="Webhook Inventory" title="Current subscriptions">
      {props.subscriptions.length === 0 ? (
        <p className={styles.empty}>Сначала загрузите tenant scope или создайте первую subscription.</p>
      ) : (
        <div className={styles.list}>
          {props.subscriptions.map((subscription) => {
            const isSelected = subscription.subscriptionId === props.selectedSubscriptionId;
            return (
              <article
                key={subscription.subscriptionId}
                className={[styles.card, isSelected ? styles.selected : ""].join(" ")}
              >
                <div className={styles.cardHeader}>
                  <div>
                    <strong>{subscription.status}</strong>
                    <p>{subscription.endpointUrl}</p>
                  </div>
                  <Button kind={isSelected ? "primary" : "secondary"} onClick={() => props.onSelect(subscription)}>
                    {isSelected ? "Editing" : "Edit subscription"}
                  </Button>
                </div>

                <dl className={styles.meta}>
                  <div>
                    <dt>Application</dt>
                    <dd>{subscription.applicationClientId}</dd>
                  </div>
                  <div>
                    <dt>Events</dt>
                    <dd>{subscription.eventTypes.join(", ")}</dd>
                  </div>
                  <div>
                    <dt>Updated</dt>
                    <dd>{subscription.updatedUtc ?? subscription.createdUtc}</dd>
                  </div>
                </dl>
              </article>
            );
          })}
        </div>
      )}
    </Panel>
  );
}
