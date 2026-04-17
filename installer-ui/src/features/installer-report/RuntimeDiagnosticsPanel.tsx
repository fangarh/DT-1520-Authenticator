import type { InstallerExecutionReport, InstallerTroubleshootingHint } from "../../shared/types/installer-contracts";
import { StatusBadge } from "../../shared/ui/StatusBadge";
import styles from "./InstallerReportView.module.css";

interface RuntimeDiagnosticsPanelProps {
  report: InstallerExecutionReport;
}

const getHintTone = (hint: InstallerTroubleshootingHint) => {
  switch (hint.severity) {
    case "error":
      return "danger";
    case "warning":
      return "warning";
    default:
      return "neutral";
  }
};

export function RuntimeDiagnosticsPanel({ report }: RuntimeDiagnosticsPanelProps) {
  return (
    <section className={styles.section}>
      <div className={styles.sectionHeader}>
        <h3>Runtime diagnostics</h3>
        <span>{report.runtimeStatus.services.length} services</span>
      </div>

      <div className={styles.serviceGrid}>
        {report.runtimeStatus.services.map((service) => (
          <article key={service.service} className={styles.serviceCard}>
            <div className={styles.stepMeta}>
              <strong>{service.service}</strong>
              <StatusBadge
                tone={service.health === "healthy" ? "success" : service.health ? "warning" : "neutral"}
                label={service.health ?? service.state}
              />
            </div>
            <dl className={styles.inlineFacts}>
              <div>
                <dt>State</dt>
                <dd>{service.state}</dd>
              </div>
              <div>
                <dt>Exit code</dt>
                <dd>{service.exitCode ?? "n/a"}</dd>
              </div>
            </dl>
            {service.publishers.length > 0 ? (
              <ul className={styles.publisherList}>
                {service.publishers.map((publisher) => (
                  <li key={`${publisher.url}-${publisher.targetPort}`}>
                    {publisher.url} ({publisher.protocol})
                  </li>
                ))}
              </ul>
            ) : (
              <p className={styles.muted}>No published ports.</p>
            )}
          </article>
        ))}
      </div>

      {report.workerDiagnostics ? (
        <article className={styles.workerCard}>
          <div className={styles.stepMeta}>
            <strong>{report.workerDiagnostics.serviceName}</strong>
            <StatusBadge
              tone={report.workerDiagnostics.executionOutcome === "healthy" ? "success" : "warning"}
              label={report.workerDiagnostics.executionOutcome}
            />
          </div>
          <dl className={styles.inlineFacts}>
            <div>
              <dt>Last heartbeat</dt>
              <dd>{report.workerDiagnostics.lastHeartbeatUtc}</dd>
            </div>
            <div>
              <dt>Failures</dt>
              <dd>{report.workerDiagnostics.consecutiveFailureCount}</dd>
            </div>
          </dl>
          <div className={styles.workerColumns}>
            <div>
              <h4>Dependencies</h4>
              <ul className={styles.simpleList}>
                {report.workerDiagnostics.dependencyStatuses.map((dependency) => (
                  <li key={dependency.name}>
                    {dependency.name}: {dependency.status}
                    {dependency.failureKind ? ` (${dependency.failureKind})` : ""}
                  </li>
                ))}
              </ul>
            </div>
            <div>
              <h4>Jobs</h4>
              <ul className={styles.simpleList}>
                {report.workerDiagnostics.jobStatuses.map((job) => (
                  <li key={job.name}>
                    {job.name}: {job.status}
                    {job.failureKind ? ` (${job.failureKind})` : ""}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </article>
      ) : null}

      {report.troubleshootingHints.length > 0 ? (
        <div className={styles.hintList}>
          {report.troubleshootingHints.map((hint) => (
            <article key={`${hint.code}-${hint.component ?? "root"}`} className={styles.hintCard}>
              <div className={styles.stepMeta}>
                <strong>{hint.message}</strong>
                <StatusBadge tone={getHintTone(hint)} label={hint.severity} />
              </div>
              <p className={styles.muted}>{hint.recommendedAction}</p>
            </article>
          ))}
        </div>
      ) : null}
    </section>
  );
}
