import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import type { AdminIntegrationClientView } from "../../shared/types/admin-contracts";
import { IntegrationClientStatusBadge } from "./IntegrationClientStatusBadge";
import styles from "./IntegrationClientListPanel.module.css";

interface IntegrationClientListPanelProps {
  clients: AdminIntegrationClientView[];
  selectedClientId: string | null;
  onSelect: (client: AdminIntegrationClientView) => void;
}

export function IntegrationClientListPanel(props: IntegrationClientListPanelProps) {
  return (
    <Panel eyebrow="Client Inventory" title="Current clients">
      {props.clients.length === 0 ? (
        <p className={styles.empty}>Сначала загрузите tenant scope или создайте первый integration client.</p>
      ) : (
        <div className={styles.list}>
          {props.clients.map((client) => {
            const isSelected = client.clientId === props.selectedClientId;
            return (
              <article
                key={client.clientId}
                className={[styles.card, isSelected ? styles.selected : ""].join(" ")}
              >
                <div className={styles.cardHeader}>
                  <div className={styles.titleBlock}>
                    <strong>{client.clientId}</strong>
                    <IntegrationClientStatusBadge status={client.status} />
                  </div>
                  <Button kind={isSelected ? "primary" : "secondary"} onClick={() => props.onSelect(client)}>
                    {isSelected ? "Selected" : "Inspect client"}
                  </Button>
                </div>

                <dl className={styles.meta}>
                  <div>
                    <dt>Application</dt>
                    <dd>{client.applicationClientId}</dd>
                  </div>
                  <div>
                    <dt>Scopes</dt>
                    <dd>{client.allowedScopes.join(", ")}</dd>
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
