import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import type { AdminTenantDirectoryTenantView } from "../../shared/types/admin-contracts";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { TenantDirectoryStatusBadge } from "./TenantDirectoryStatusBadge";
import styles from "./TenantDirectoryListPanel.module.css";

interface TenantDirectoryListPanelProps {
  tenants: AdminTenantDirectoryTenantView[];
  selectedTenantId: string | null;
  pending: boolean;
  canRead: boolean;
  onLoad: () => Promise<void>;
  onSelect: (tenant: AdminTenantDirectoryTenantView) => Promise<void>;
}

export function TenantDirectoryListPanel(props: TenantDirectoryListPanelProps) {
  return (
    <Panel
      eyebrow="Tenants"
      title="Organizations"
      aside={
        <Button kind="secondary" onClick={() => void props.onLoad()} disabled={props.pending || !props.canRead}>
          {props.pending ? "Loading..." : "Load tenants"}
        </Button>
      }
    >
      {props.tenants.length === 0 ? (
        <p className={styles.empty}>Загрузите tenant directory или создайте первый tenant через quick create.</p>
      ) : (
        <div className={styles.table} role="table" aria-label="Tenant directory">
          <div className={styles.header} role="row">
            <span role="columnheader">Organization</span>
            <span role="columnheader">Status</span>
            <span role="columnheader">Apps</span>
            <span role="columnheader">Clients</span>
            <span role="columnheader">Updated</span>
            <span role="columnheader">Action</span>
          </div>
          {props.tenants.map((tenant) => {
            const selected = tenant.tenantId === props.selectedTenantId;
            return (
              <article
                key={tenant.tenantId}
                className={[styles.row, selected ? styles.selected : ""].join(" ")}
                role="row"
              >
                <div className={styles.identity} role="cell">
                  <strong>{tenant.displayName}</strong>
                  <code>{tenant.tenantId}</code>
                  {tenant.slug ? <small>{tenant.slug}</small> : null}
                </div>
                <span role="cell"><TenantDirectoryStatusBadge status={tenant.status} /></span>
                <span role="cell">{tenant.applicationCount}</span>
                <span role="cell">{tenant.integrationClientCount}</span>
                <span role="cell">{formatUtcInstant(tenant.updatedUtc ?? tenant.createdUtc)}</span>
                <span role="cell">
                  <Button kind={selected ? "primary" : "secondary"} onClick={() => void props.onSelect(tenant)}>
                    {selected ? "Selected" : "Open"}
                  </Button>
                </span>
              </article>
            );
          })}
        </div>
      )}
    </Panel>
  );
}
