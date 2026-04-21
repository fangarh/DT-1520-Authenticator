import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import { deliveryChannelOptions, deliveryStatusOptions } from "./model/delivery-filter-options";
import styles from "./DeliveryStatusLookupPanel.module.css";

interface DeliveryStatusLookupPanelProps {
  tenantId: string;
  applicationClientId: string;
  channel: string;
  status: string;
  limit: string;
  pending: boolean;
  canRead: boolean;
  onTenantIdChange: (value: string) => void;
  onApplicationClientIdChange: (value: string) => void;
  onChannelChange: (value: string) => void;
  onStatusChange: (value: string) => void;
  onLimitChange: (value: string) => void;
  onSubmit: () => Promise<void>;
}

export function DeliveryStatusLookupPanel(props: DeliveryStatusLookupPanelProps) {
  return (
    <Panel
      eyebrow="Delivery Scope"
      title="Load recent deliveries"
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
          <span>Application Client Filter</span>
          <input
            value={props.applicationClientId}
            onChange={(event) => props.onApplicationClientIdChange(event.target.value)}
            placeholder="Optional GUID filter"
          />
        </label>

        <div className={styles.filterGrid}>
          <label className={styles.field}>
            <span>Channel</span>
            <select value={props.channel} onChange={(event) => props.onChannelChange(event.target.value)}>
              <option value="">all channels</option>
              {deliveryChannelOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <label className={styles.field}>
            <span>Status</span>
            <select value={props.status} onChange={(event) => props.onStatusChange(event.target.value)}>
              <option value="">all statuses</option>
              {deliveryStatusOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <label className={styles.field}>
            <span>Limit</span>
            <input
              type="number"
              min={1}
              max={200}
              value={props.limit}
              onChange={(event) => props.onLimitChange(event.target.value)}
              placeholder="25"
            />
          </label>
        </div>

        <p className={styles.hint}>
          Surface остается read-only: только sanitized destination, outcome и timing metadata без payload replay.
        </p>

        <Button onClick={() => void props.onSubmit()} disabled={props.pending || !props.canRead} stretch>
          {props.pending ? "Loading..." : "Load deliveries"}
        </Button>
      </div>
    </Panel>
  );
}
