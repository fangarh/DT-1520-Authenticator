import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import styles from "./UserDeviceLookupPanel.module.css";

interface UserDeviceLookupPanelProps {
  tenantId: string;
  externalUserId: string;
  pending: boolean;
  canRead: boolean;
  onTenantIdChange: (value: string) => void;
  onExternalUserIdChange: (value: string) => void;
  onSubmit: () => Promise<void>;
}

export function UserDeviceLookupPanel(props: UserDeviceLookupPanelProps) {
  return (
    <Panel
      eyebrow="Device Scope"
      title="Load user devices"
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
            placeholder="user@example.local"
          />
        </label>

        <p className={styles.hint}>
          Surface отдает только safe metadata: lifecycle state, platform, push capability и timestamps без `installationId` и device secrets.
        </p>

        <Button onClick={() => void props.onSubmit()} disabled={props.pending || !props.canRead} stretch>
          {props.pending ? "Loading..." : "Load devices"}
        </Button>
      </div>
    </Panel>
  );
}
