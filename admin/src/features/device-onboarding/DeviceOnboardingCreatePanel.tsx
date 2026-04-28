import type { AdminDeviceOnboardingPlatform } from "../../shared/types/admin-contracts";
import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import type { DeviceOnboardingPendingAction } from "./model/deviceOnboardingWorkspaceModel";
import styles from "./DeviceOnboardingCreatePanel.module.css";

interface DeviceOnboardingCreatePanelProps {
  tenantId: string;
  applicationClientId: string;
  externalUserId: string;
  platform: AdminDeviceOnboardingPlatform;
  ttlMinutes: string;
  pending: DeviceOnboardingPendingAction;
  canWrite: boolean;
  onTenantIdChange: (value: string) => void;
  onApplicationClientIdChange: (value: string) => void;
  onExternalUserIdChange: (value: string) => void;
  onPlatformChange: (value: AdminDeviceOnboardingPlatform) => void;
  onTtlMinutesChange: (value: string) => void;
  onCreate: () => Promise<void>;
  onReset: () => void;
}

export function DeviceOnboardingCreatePanel(props: DeviceOnboardingCreatePanelProps) {
  return (
    <Panel
      eyebrow="QR Onboarding"
      title="Issue device QR"
      aside={<span className={styles.scope}>{props.canWrite ? "write enabled" : "write missing"}</span>}
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
          <span>Application Client ID</span>
          <input
            value={props.applicationClientId}
            onChange={(event) => props.onApplicationClientIdChange(event.target.value)}
            placeholder="f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"
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

        <div className={styles.options}>
          <label className={styles.field}>
            <span>Platform</span>
            <select
              value={props.platform}
              onChange={(event) => props.onPlatformChange(event.target.value as AdminDeviceOnboardingPlatform)}
            >
              <option value="android">android</option>
              <option value="ios">ios</option>
              <option value="unknown">unknown</option>
            </select>
          </label>

          <label className={styles.field}>
            <span>TTL minutes</span>
            <input
              value={props.ttlMinutes}
              inputMode="numeric"
              onChange={(event) => props.onTtlMinutesChange(event.target.value)}
              placeholder="15"
            />
          </label>
        </div>

        <div className={styles.actions}>
          <Button onClick={() => void props.onCreate()} disabled={props.pending === "create" || !props.canWrite} stretch>
            {props.pending === "create" ? "Creating..." : "Create QR"}
          </Button>
          <Button kind="secondary" onClick={props.onReset} disabled={props.pending === "create"}>
            Clear form
          </Button>
        </div>
      </div>
    </Panel>
  );
}
