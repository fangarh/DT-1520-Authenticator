import type { AdminDeviceOnboardingStatus } from "../../shared/types/admin-contracts";
import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import styles from "./DeviceOnboardingLookupPanel.module.css";

interface DeviceOnboardingLookupPanelProps {
  tenantId: string;
  externalUserId: string;
  applicationClientId: string;
  status: AdminDeviceOnboardingStatus | "";
  limit: string;
  pending: boolean;
  canRead: boolean;
  onTenantIdChange: (value: string) => void;
  onExternalUserIdChange: (value: string) => void;
  onApplicationClientIdChange: (value: string) => void;
  onStatusChange: (value: AdminDeviceOnboardingStatus | "") => void;
  onLimitChange: (value: string) => void;
  onSubmit: () => Promise<void>;
}

export function DeviceOnboardingLookupPanel(props: DeviceOnboardingLookupPanelProps) {
  return (
    <Panel
      eyebrow="QR Inventory"
      title="Load onboarding artifacts"
      aside={<span className={styles.scope}>{props.canRead ? "read enabled" : "read missing"}</span>}
    >
      <div className={styles.form}>
        <label className={styles.field}>
          <span>Tenant ID</span>
          <input
            value={props.tenantId}
            onChange={(event) => props.onTenantIdChange(event.target.value)}
            placeholder="6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"
          />
        </label>

        <label className={styles.field}>
          <span>External User ID</span>
          <input
            value={props.externalUserId}
            onChange={(event) => props.onExternalUserIdChange(event.target.value)}
            placeholder="keycloak-sub-or-user-id"
          />
        </label>

        <label className={styles.field}>
          <span>Application Client ID</span>
          <input
            value={props.applicationClientId}
            onChange={(event) => props.onApplicationClientIdChange(event.target.value)}
            placeholder="f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"
          />
        </label>

        <div className={styles.filterRow}>
          <label className={styles.field}>
            <span>Status</span>
            <select
              value={props.status}
              onChange={(event) => props.onStatusChange(event.target.value as AdminDeviceOnboardingStatus | "")}
            >
              <option value="">any status</option>
              <option value="pending">pending artifacts</option>
              <option value="consumed">consumed artifacts</option>
              <option value="expired">expired artifacts</option>
              <option value="revoked">revoked artifacts</option>
            </select>
          </label>

          <label className={styles.field}>
            <span>Limit</span>
            <input
              value={props.limit}
              inputMode="numeric"
              onChange={(event) => props.onLimitChange(event.target.value)}
              placeholder="50"
            />
          </label>
        </div>

        <Button onClick={() => void props.onSubmit()} disabled={props.pending || !props.canRead} stretch>
          {props.pending ? "Loading..." : "Load QR artifacts"}
        </Button>
      </div>
    </Panel>
  );
}
