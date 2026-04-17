import { StatusBadge } from "../../shared/ui/StatusBadge";
import type { InstallerExecutionReport } from "../../shared/types/installer-contracts";
import { getAdminUiUrl, getInstallerFlowOutcomeCopy } from "./report-handoff";
import styles from "./InstallerReportView.module.css";

interface InstallerNextStepsPanelProps {
  report: InstallerExecutionReport;
}

export function InstallerNextStepsPanel({ report }: InstallerNextStepsPanelProps) {
  const adminUiUrl = getAdminUiUrl(report);
  const flowCopy = getInstallerFlowOutcomeCopy(report);

  return (
    <section className={styles.section}>
      <div className={styles.sectionHeader}>
        <h3>Operational closure</h3>
        <span>{report.operationProfile?.displayName ?? report.mode.toLowerCase()}</span>
      </div>

      <div className={styles.handoffGrid}>
        <article className={styles.handoffCard}>
          <div className={styles.stepMeta}>
            <strong>{flowCopy.title}</strong>
            <StatusBadge tone={report.outcome === "succeeded" ? "success" : report.outcome === "degraded" ? "warning" : "danger"} label={report.outcome} />
          </div>
          <p className={styles.muted}>{flowCopy.detail}</p>
          <dl className={styles.inlineFacts}>
            <div>
              <dt>Env file</dt>
              <dd>{report.configuration?.envFilePath ?? report.manifest.paths.envFilePath}</dd>
            </div>
            <div>
              <dt>Compose file</dt>
              <dd>{report.configuration?.composeFilePath ?? report.manifest.paths.composeFilePath}</dd>
            </div>
            <div>
              <dt>Bootstrap admin</dt>
              <dd>{report.configuration?.bootstrapAdminUsername ?? report.manifest.bootstrapAdmin.username}</dd>
            </div>
          </dl>
        </article>

        <article className={styles.handoffCard}>
          <div className={styles.stepMeta}>
            <strong>Runtime handoff</strong>
            <StatusBadge tone={adminUiUrl ? "success" : "neutral"} label={adminUiUrl ? "ready" : "planned"} />
          </div>
          <p className={styles.muted}>
            The local shell hands off to the runtime Admin UI only after the script-first engine has completed its own contract.
          </p>
          {adminUiUrl ? (
            <a className={styles.handoffLink} href={adminUiUrl} rel="noreferrer" target="_blank">
              Open local Admin UI
            </a>
          ) : (
            <p className={styles.muted}>Admin UI URL becomes actionable after a live runtime startup exposes the HTTPS edge.</p>
          )}
        </article>
      </div>

      <ol className={styles.nextStepList}>
        {flowCopy.nextSteps.map((step) => (
          <li key={step}>{step}</li>
        ))}
      </ol>
    </section>
  );
}
