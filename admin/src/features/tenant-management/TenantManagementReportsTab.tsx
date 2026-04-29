import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { Button } from "../../shared/ui/Button";
import type { TenantManagementWorkspaceState } from "./model/useTenantManagementWorkspace";
import styles from "./TenantManagementWorkspace.module.css";

interface TenantManagementReportsTabProps {
  workspace: TenantManagementWorkspaceState;
}

export function TenantManagementReportsTab({ workspace }: TenantManagementReportsTabProps) {
  const summary = workspace.reportSummary;

  return (
    <div className={styles.stack} role="tabpanel">
      <section className={styles.section}>
        <h3>MVP reporting summary</h3>
        <div className={styles.actions}>
          <Button onClick={() => void workspace.loadReports()} disabled={!workspace.canReadDeliveries || workspace.pendingAction === "loadReports"}>
            {workspace.pendingAction === "loadReports" ? "Refreshing..." : "Refresh report snapshot"}
          </Button>
          <Button kind="secondary" onClick={() => void workspace.loadDevices()} disabled={!workspace.canReadDevices || workspace.pendingAction === "loadDevices"}>
            Refresh selected-user devices
          </Button>
        </div>

        <div className={styles.summary}>
          <div className={styles.summaryItem}>
            <span>Devices</span>
            <strong>{summary.devicesTotal}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>Active devices</span>
            <strong>{summary.activeDevices}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>Inactive devices</span>
            <strong>{summary.inactiveDevices}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>Deliveries</span>
            <strong>{summary.deliveriesTotal}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>Delivered</span>
            <strong>{summary.delivered}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>Failed</span>
            <strong>{summary.failed}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>Queued</span>
            <strong>{summary.queued}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>Callback delivery</span>
            <strong>{summary.callbackDeliveries}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>Webhook delivery</span>
            <strong>{summary.webhookDeliveries}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>QR artifacts</span>
            <strong>{summary.qrArtifactsRecent}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>QR pending</span>
            <strong>{summary.qrByStatus.pending}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>QR consumed</span>
            <strong>{summary.qrByStatus.consumed}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>QR expired/revoked</span>
            <strong>{summary.qrByStatus.expired + summary.qrByStatus.revoked}</strong>
          </div>
          <div className={styles.summaryItem}>
            <span>QR issued in session</span>
            <strong>{summary.qrArtifactsIssuedInSession}</strong>
          </div>
        </div>
      </section>

      <section className={styles.section}>
        <h3>Activity markers</h3>
        <dl className={styles.meta}>
          <div>
            <dt>Last approved callback</dt>
            <dd>{formatUtcInstant(summary.lastSuccessfulApprovalAtUtc)}</dd>
          </div>
          <div>
            <dt>Last failed approval delivery</dt>
            <dd>{formatUtcInstant(summary.lastFailedApprovalAtUtc)}</dd>
          </div>
          <div>
            <dt>Last selected-user device activity</dt>
            <dd>{formatUtcInstant(summary.lastDeviceSeenAtUtc)}</dd>
          </div>
        </dl>
      </section>

      <section className={styles.section}>
        <h3>Recent QR onboarding artifacts</h3>
        {workspace.recentQrArtifacts.length === 0 ? (
          <p className={styles.empty}>No QR artifact read model loaded for this tenant/user context.</p>
        ) : (
          <div className={styles.stack}>
            {workspace.recentQrArtifacts.map((artifact) => (
              <article key={artifact.activationCodeId} className={styles.row}>
                <strong>{artifact.status} / {artifact.platform}</strong>
                <code>{artifact.activationCodeId}</code>
                <span>{artifact.externalUserId}</span>
                <span>Expires {formatUtcInstant(artifact.expiresAtUtc)}</span>
              </article>
            ))}
          </div>
        )}
      </section>

      <section className={styles.section}>
        <h3>Recent delivery outcomes</h3>
        {workspace.deliveries.length === 0 ? (
          <p className={styles.empty}>No delivery outcomes loaded for this tenant context.</p>
        ) : (
          <div className={styles.stack}>
            {workspace.deliveries.map((delivery) => (
              <article key={delivery.deliveryId} className={styles.row}>
                <strong>{delivery.eventType} / {delivery.status}</strong>
                <code>{delivery.channel}</code>
                <span>{delivery.deliveryDestination}</span>
                <span>{formatUtcInstant(delivery.createdAtUtc)}</span>
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
