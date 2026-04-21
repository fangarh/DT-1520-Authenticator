import { Button } from "../../shared/ui/Button";
import type { AdminDeliveryStatusView } from "../../shared/types/admin-contracts";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { Panel } from "../../shared/ui/Panel";
import { DeliveryStatusBadge } from "./DeliveryStatusBadge";
import styles from "./DeliveryStatusListPanel.module.css";

interface DeliveryStatusListPanelProps {
  deliveries: AdminDeliveryStatusView[];
  selectedDeliveryId: string | null;
  onSelect: (delivery: AdminDeliveryStatusView) => void;
}

export function DeliveryStatusListPanel(props: DeliveryStatusListPanelProps) {
  return (
    <Panel eyebrow="Delivery Inventory" title="Recent callback and webhook outcomes">
      {props.deliveries.length === 0 ? (
        <p className={styles.empty}>Сначала загрузите tenant scope, чтобы увидеть последние delivery outcomes.</p>
      ) : (
        <div className={styles.list}>
          {props.deliveries.map((delivery) => {
            const isSelected = delivery.deliveryId === props.selectedDeliveryId;
            return (
              <article
                key={delivery.deliveryId}
                className={[styles.card, isSelected ? styles.selected : ""].join(" ")}
              >
                <div className={styles.cardHeader}>
                  <div className={styles.headerSummary}>
                    <DeliveryStatusBadge status={delivery.status} />
                    <span className={styles.channel}>{delivery.channel}</span>
                    {delivery.isRetryScheduled ? <span className={styles.retry}>retry scheduled</span> : null}
                  </div>
                  <Button kind={isSelected ? "primary" : "secondary"} onClick={() => props.onSelect(delivery)}>
                    {isSelected ? "Selected" : "Inspect"}
                  </Button>
                </div>

                <strong className={styles.eventType}>{delivery.eventType}</strong>
                <p className={styles.destination}>{delivery.deliveryDestination}</p>

                <dl className={styles.meta}>
                  <div>
                    <dt>Subject</dt>
                    <dd>{delivery.subjectType}</dd>
                  </div>
                  <div>
                    <dt>Attempts</dt>
                    <dd>{delivery.attemptCount}</dd>
                  </div>
                  <div>
                    <dt>Occurred</dt>
                    <dd>{formatUtcInstant(delivery.occurredAtUtc)}</dd>
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
