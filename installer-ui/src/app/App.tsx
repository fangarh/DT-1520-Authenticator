import { useDeferredValue } from "react";
import { InstallerReportView } from "../features/installer-report/InstallerReportView";
import { InstallerRunForm } from "../features/installer-shell/InstallerRunForm";
import { useInstallerShell } from "../features/installer-shell/model/useInstallerShell";
import { Notice } from "../shared/ui/Notice";
import styles from "./App.module.css";

export default function App() {
  const shell = useInstallerShell();
  const deferredReport = useDeferredValue(shell.report);

  return (
    <main className={styles.shell}>
      <section className={styles.hero}>
        <div className={styles.heroCopy}>
          <p className={styles.eyebrow}>OTPAuth Installer</p>
          <h1>Separate local setup shell over the script-first engine.</h1>
          <p className={styles.subtitle}>
            Iteration 4 closes the operator happy path for `install/update/recover`: the browser stays local-only, preserves `ADR-025/029`,
            and now carries the run through guardrails, sanitized report analysis and runtime handoff.
          </p>
        </div>

        <aside className={styles.metaGrid}>
          <article className={styles.metaCard}>
            <span>Plane</span>
            <strong>bootstrap/setup</strong>
            <small>never the runtime `admin/` contour</small>
          </article>
          <article className={styles.metaCard}>
            <span>Transport</span>
            <strong>{shell.shellInfo?.transport ?? "bootstrapping"}</strong>
            <small>bound to `127.0.0.1`</small>
          </article>
          <article className={styles.metaCard}>
            <span>Execution</span>
            <strong>{shell.shellInfo?.executionMode ?? "loading"}</strong>
            <small>script engine remains source of truth</small>
          </article>
        </aside>
      </section>

      {shell.notice ? (
        <Notice
          actionHint={shell.notice.actionHint}
          detail={shell.notice.detail}
          title={shell.notice.title}
          tone={shell.notice.tone}
        />
      ) : null}

      <section className={styles.layout}>
        <InstallerRunForm
          draft={shell.draft}
          loadingShellInfo={shell.loadingShellInfo}
          pending={shell.pending}
          shellInfo={shell.shellInfo}
          onDraftChange={(updater) => shell.setDraft(updater)}
          onPermissionEnabled={shell.setPermissionEnabled}
          onSubmit={shell.runInstaller}
        />
        <InstallerReportView report={deferredReport} />
      </section>
    </main>
  );
}
