import { Panel } from "../../shared/ui/Panel";
import type { AdminTenantDirectoryDetailView } from "../../shared/types/admin-contracts";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { TenantDirectoryStatusBadge } from "./TenantDirectoryStatusBadge";
import styles from "./TenantDirectoryDetailPanel.module.css";

interface TenantDirectoryDetailPanelProps {
  directory: AdminTenantDirectoryDetailView | null;
}

export function TenantDirectoryDetailPanel({ directory }: TenantDirectoryDetailPanelProps) {
  if (!directory) {
    return (
      <Panel eyebrow="Tenant Context" title="Directory detail">
        <p className={styles.empty}>Выберите tenant, чтобы увидеть application и API client context.</p>
      </Panel>
    );
  }

  return (
    <Panel
      eyebrow="Tenant Context"
      title={directory.tenant.displayName}
      aside={<TenantDirectoryStatusBadge status={directory.tenant.status} />}
    >
      <div className={styles.layout}>
        <dl className={styles.meta}>
          <div>
            <dt>Tenant ID</dt>
            <dd>{directory.tenant.tenantId}</dd>
          </div>
          <div>
            <dt>Slug</dt>
            <dd>{directory.tenant.slug ?? "n/a"}</dd>
          </div>
          <div>
            <dt>Created</dt>
            <dd>{formatUtcInstant(directory.tenant.createdUtc)}</dd>
          </div>
          <div>
            <dt>Updated</dt>
            <dd>{formatUtcInstant(directory.tenant.updatedUtc)}</dd>
          </div>
        </dl>

        <section className={styles.subsection}>
          <h3>Applications</h3>
          {directory.applications.length === 0 ? (
            <p className={styles.empty}>No applications registered.</p>
          ) : (
            <div className={styles.stack}>
              {directory.applications.map((application) => (
                <article key={application.applicationClientId} className={styles.item}>
                  <strong>{application.displayName}</strong>
                  <code>{application.applicationClientId}</code>
                  <span>{application.integrationClientCount} client(s)</span>
                </article>
              ))}
            </div>
          )}
        </section>

        <section className={styles.subsection}>
          <h3>API clients</h3>
          {directory.integrationClients.length === 0 ? (
            <p className={styles.empty}>No API clients registered.</p>
          ) : (
            <div className={styles.stack}>
              {directory.integrationClients.map((client) => (
                <article key={client.clientId} className={styles.item}>
                  <strong>{client.clientId}</strong>
                  <code>{client.applicationClientId}</code>
                  <span>{client.allowedScopes.join(", ")}</span>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>
    </Panel>
  );
}
