import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import type { AdminIntegrationClientView } from "../../shared/types/admin-contracts";
import { Panel } from "../../shared/ui/Panel";
import { IntegrationClientStatusBadge } from "./IntegrationClientStatusBadge";
import styles from "./IntegrationClientDetailPanel.module.css";

interface IntegrationClientDetailPanelProps {
  client: AdminIntegrationClientView | null;
}

export function IntegrationClientDetailPanel({ client }: IntegrationClientDetailPanelProps) {
  return (
    <Panel
      eyebrow="Client Detail"
      title="Sanitized metadata"
      aside={client ? <IntegrationClientStatusBadge status={client.status} /> : undefined}
    >
      {!client ? (
        <p className={styles.empty}>Выберите integration client из списка, чтобы увидеть tenant binding, scopes и timestamps.</p>
      ) : (
        <div className={styles.layout}>
          <div className={styles.summary}>
            <strong>{client.clientId}</strong>
            <p>{client.tenantId}</p>
          </div>

          <dl className={styles.meta}>
            <div>
              <dt>Application Client</dt>
              <dd>{client.applicationClientId}</dd>
            </div>
            <div>
              <dt>Allowed scopes</dt>
              <dd>{client.allowedScopes.join(", ")}</dd>
            </div>
            <div>
              <dt>Created</dt>
              <dd>{formatUtcInstant(client.createdUtc)}</dd>
            </div>
            <div>
              <dt>Updated</dt>
              <dd>{formatUtcInstant(client.updatedUtc)}</dd>
            </div>
            <div>
              <dt>Secret rotated</dt>
              <dd>{formatUtcInstant(client.lastSecretRotatedUtc)}</dd>
            </div>
            <div>
              <dt>Auth state changed</dt>
              <dd>{formatUtcInstant(client.lastAuthStateChangedUtc)}</dd>
            </div>
          </dl>
        </div>
      )}
    </Panel>
  );
}
