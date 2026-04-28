import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import type { AdminTenantDirectoryStatus } from "../../shared/types/admin-contracts";
import { tenantStatusOptions, type TenantDirectoryPendingAction } from "./model/tenantDirectoryWorkspaceModel";
import styles from "./TenantManualCreatePanel.module.css";

interface TenantManualCreatePanelProps {
  tenantId: string;
  displayName: string;
  slug: string;
  status: AdminTenantDirectoryStatus;
  pending: TenantDirectoryPendingAction;
  canWrite: boolean;
  onTenantIdChange: (value: string) => void;
  onDisplayNameChange: (value: string) => void;
  onSlugChange: (value: string) => void;
  onStatusChange: (value: AdminTenantDirectoryStatus) => void;
  onCreate: () => Promise<void>;
  onReset: () => void;
}

export function TenantManualCreatePanel(props: TenantManualCreatePanelProps) {
  return (
    <Panel eyebrow="Advanced" title="Manual tenant create">
      <div className={styles.form}>
        <label className={styles.field}>
          <span>Tenant ID</span>
          <input
            value={props.tenantId}
            onChange={(event) => props.onTenantIdChange(event.target.value)}
            placeholder="Optional server-generated when empty"
          />
        </label>

        <label className={styles.field}>
          <span>Display name</span>
          <input
            value={props.displayName}
            onChange={(event) => props.onDisplayNameChange(event.target.value)}
            placeholder="Migration Tenant"
          />
        </label>

        <label className={styles.field}>
          <span>Slug</span>
          <input
            value={props.slug}
            onChange={(event) => props.onSlugChange(event.target.value)}
            placeholder="Optional normalized slug"
          />
        </label>

        <label className={styles.field}>
          <span>Status</span>
          <select
            value={props.status}
            onChange={(event) => props.onStatusChange(event.target.value as AdminTenantDirectoryStatus)}
          >
            {tenantStatusOptions.map((status) => (
              <option key={status} value={status}>{status}</option>
            ))}
          </select>
        </label>

        <div className={styles.actions}>
          <Button onClick={() => void props.onCreate()} disabled={props.pending === "manualCreate" || !props.canWrite} stretch>
            {props.pending === "manualCreate" ? "Creating..." : "Create tenant"}
          </Button>
          <Button kind="secondary" onClick={props.onReset} disabled={props.pending === "manualCreate"}>
            Clear form
          </Button>
        </div>
      </div>
    </Panel>
  );
}
