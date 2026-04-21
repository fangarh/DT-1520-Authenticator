import type { AdminDeliveryStatusView } from "../../shared/types/admin-contracts";
import { Panel } from "../../shared/ui/Panel";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { DeliveryStatusBadge } from "./DeliveryStatusBadge";
import styles from "./DeliveryStatusDetailPanel.module.css";

interface DeliveryStatusDetailPanelProps {
  delivery: AdminDeliveryStatusView | null;
}

export function DeliveryStatusDetailPanel({ delivery }: DeliveryStatusDetailPanelProps) {
  return (
    <Panel
      eyebrow="Delivery Detail"
      title="Selected outcome"
      aside={delivery ? <DeliveryStatusBadge status={delivery.status} /> : undefined}
    >
      {!delivery ? (
        <p className={styles.empty}>Выберите delivery record из списка, чтобы увидеть sanitized detail panel.</p>
      ) : (
        <div className={styles.layout}>
          <div className={styles.summary}>
            <strong className={styles.eventType}>{delivery.eventType}</strong>
            <p className={styles.destination}>{delivery.deliveryDestination}</p>
            <div className={styles.flagRow}>
              <span className={styles.channel}>{delivery.channel}</span>
              <span className={styles.subject}>{delivery.subjectType}</span>
              {delivery.isRetryScheduled ? <span className={styles.retry}>retry scheduled</span> : null}
            </div>
          </div>

          <dl className={styles.meta}>
            <div>
              <dt>Delivery ID</dt>
              <dd>{delivery.deliveryId}</dd>
            </div>
            <div>
              <dt>Tenant</dt>
              <dd>{delivery.tenantId}</dd>
            </div>
            <div>
              <dt>Application</dt>
              <dd>{delivery.applicationClientId}</dd>
            </div>
            <div>
              <dt>Subject ID</dt>
              <dd>{delivery.subjectId}</dd>
            </div>
            <div>
              <dt>Publication ID</dt>
              <dd>{delivery.publicationId ?? "n/a"}</dd>
            </div>
            <div>
              <dt>Attempts</dt>
              <dd>{delivery.attemptCount}</dd>
            </div>
            <div>
              <dt>Occurred</dt>
              <dd>{formatUtcInstant(delivery.occurredAtUtc)}</dd>
            </div>
            <div>
              <dt>Created</dt>
              <dd>{formatUtcInstant(delivery.createdAtUtc)}</dd>
            </div>
            <div>
              <dt>Last attempt</dt>
              <dd>{formatUtcInstant(delivery.lastAttemptAtUtc)}</dd>
            </div>
            <div>
              <dt>Next attempt</dt>
              <dd>{formatUtcInstant(delivery.nextAttemptAtUtc)}</dd>
            </div>
            <div>
              <dt>Delivered at</dt>
              <dd>{formatUtcInstant(delivery.deliveredAtUtc)}</dd>
            </div>
            <div>
              <dt>Last error code</dt>
              <dd>{delivery.lastErrorCode ?? "n/a"}</dd>
            </div>
          </dl>

          <p className={styles.hint}>
            Operator surface intentionally excludes raw payload, response body и replay actions в рамках `Iteration 1`.
          </p>
        </div>
      )}
    </Panel>
  );
}
