import { Panel } from "../../shared/ui/Panel";
import { StatusBadge } from "../../shared/ui/StatusBadge";
import type { InstallerExecutionReport } from "../../shared/types/installer-contracts";
import { InstallerNextStepsPanel } from "./InstallerNextStepsPanel";
import { RuntimeDiagnosticsPanel } from "./RuntimeDiagnosticsPanel";
import { StepResultsPanel } from "./StepResultsPanel";
import styles from "./InstallerReportView.module.css";

interface InstallerReportViewProps {
  report: InstallerExecutionReport | null;
}

const getOutcomeTone = (outcome: InstallerExecutionReport["outcome"]) => {
  switch (outcome) {
    case "succeeded":
      return "success";
    case "degraded":
      return "warning";
    case "failed":
      return "danger";
  }
};

export function InstallerReportView({ report }: InstallerReportViewProps) {
  if (!report) {
    return (
      <Panel title="Awaiting local run" eyebrow="Sanitized report">
        <div className={styles.emptyState}>
          <strong>No installer report yet.</strong>
          <p>
            This shell only becomes useful after the bridge has executed `install.ps1` and returned the sanitized JSON report from the
            engine contract.
          </p>
        </div>
      </Panel>
    );
  }

  return (
    <Panel
      title="Installer report"
      eyebrow="Engine contract"
      aside={<StatusBadge tone={getOutcomeTone(report.outcome)} label={report.outcome} />}
    >
      <div className={styles.summaryGrid}>
        <article className={styles.summaryCard}>
          <span>Mode</span>
          <strong>{report.mode}</strong>
          <small>{report.preflightOnly ? "Preflight only" : report.dryRun ? "Dry run" : "Live execution"}</small>
        </article>

        <article className={styles.summaryCard}>
          <span>Steps</span>
          <strong>
            {report.summary.succeededSteps}/{report.summary.totalSteps}
          </strong>
          <small>{report.summary.failedSteps} failed</small>
        </article>

        <article className={styles.summaryCard}>
          <span>Diagnostics</span>
          <strong>{report.summary.diagnosticIssueCount}</strong>
          <small>{report.summary.troubleshootingHintCount} hints</small>
        </article>
      </div>

      {report.validationIssues.length > 0 ? (
        <section className={styles.section}>
          <div className={styles.sectionHeader}>
            <h3>Validation issues</h3>
            <span>{report.validationIssues.length}</span>
          </div>
          <ul className={styles.simpleList}>
            {report.validationIssues.map((issue) => (
              <li key={`${issue.code}-${issue.field}`}>
                <strong>{issue.field}</strong>: {issue.message}
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <InstallerNextStepsPanel report={report} />
      <StepResultsPanel stepResults={report.stepResults} />
      <RuntimeDiagnosticsPanel report={report} />
    </Panel>
  );
}
