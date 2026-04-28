import type { AdminTenantDirectoryDetailView } from "../../shared/types/admin-contracts";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { TenantDirectoryStatusBadge } from "../tenant-directory/TenantDirectoryStatusBadge";
import styles from "./TenantManagementWorkspace.module.css";

interface TenantManagementOverviewTabProps {
  directory: AdminTenantDirectoryDetailView;
}

export function TenantManagementOverviewTab({ directory }: TenantManagementOverviewTabProps) {
  return (
    <div className={styles.stack} role="tabpanel">
      <section className={styles.section}>
        <h3>Tenant summary</h3>
        <dl className={styles.meta}>
          <div>
            <dt>Tenant ID</dt>
            <dd>{directory.tenant.tenantId}</dd>
          </div>
          <div>
            <dt>Status</dt>
            <dd><TenantDirectoryStatusBadge status={directory.tenant.status} /></dd>
          </div>
          <div>
            <dt>Applications</dt>
            <dd>{directory.applications.length}</dd>
          </div>
          <div>
            <dt>API clients</dt>
            <dd>{directory.integrationClients.length}</dd>
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
      </section>

      <section className={styles.section}>
        <h3>Application context</h3>
        {directory.applications.length === 0 ? (
          <p className={styles.empty}>No applications are registered for this tenant.</p>
        ) : (
          <div className={styles.grid}>
            {directory.applications.map((application) => (
              <article key={application.applicationClientId} className={styles.row}>
                <strong>{application.displayName}</strong>
                <code>{application.applicationClientId}</code>
                <span>{application.integrationClientCount} API client(s)</span>
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
