import type { InstallerStepResult } from "../../shared/types/installer-contracts";
import { StatusBadge } from "../../shared/ui/StatusBadge";
import styles from "./InstallerReportView.module.css";

interface StepResultsPanelProps {
  stepResults: InstallerStepResult[];
}

export function StepResultsPanel({ stepResults }: StepResultsPanelProps) {
  return (
    <section className={styles.section}>
      <div className={styles.sectionHeader}>
        <h3>Step results</h3>
        <span>{stepResults.length}</span>
      </div>

      <div className={styles.stepList}>
        {stepResults.map((step) => (
          <article key={step.stepId} className={styles.stepCard}>
            <div className={styles.stepMeta}>
              <strong>{step.name}</strong>
              <StatusBadge tone={step.status === "succeeded" ? "success" : "danger"} label={step.status} />
            </div>
            <p className={styles.stepMessage}>{step.message}</p>
            <dl className={styles.inlineFacts}>
              <div>
                <dt>Step ID</dt>
                <dd>{step.stepId}</dd>
              </div>
              <div>
                <dt>Duration</dt>
                <dd>{step.durationMs} ms</dd>
              </div>
              <div>
                <dt>Exit code</dt>
                <dd>{step.exitCode}</dd>
              </div>
            </dl>
            <code className={styles.commandPreview}>{step.commandPreview}</code>
            {step.outputLines.length > 0 ? (
              <pre className={styles.outputBlock}>{step.outputLines.join("\n")}</pre>
            ) : null}
          </article>
        ))}
      </div>
    </section>
  );
}
