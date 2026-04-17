import { LoginPanel } from "../features/auth/LoginPanel";
import { useAdminSession } from "../features/auth/useAdminSession";
import { EnrollmentWorkspace } from "../features/enrollment-workspace/EnrollmentWorkspace";
import { Button } from "../shared/ui/Button";
import styles from "./App.module.css";

export default function App() {
  const session = useAdminSession();
  const currentSession = session.status === "authenticated" ? session.current : null;

  return (
    <main className={styles.shell}>
      <section className={styles.chrome}>
        <div className={styles.hero}>
          <p className={styles.eyebrow}>OTPAuth Admin</p>
          <h1 className={styles.title}>Operator console for TOTP enrollment lifecycle.</h1>
          <p className={styles.subtitle}>
            Browser contour now talks only to `/api/v1/admin/*`: session cookie,
            CSRF, current enrollment lookup and command transport.
          </p>
        </div>

        <aside className={styles.sidebar}>
          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>Contour</span>
            <strong>admin cookie session</strong>
            <span className={styles.metaHint}>`enrollments.read` / `enrollments.write`</span>
          </div>

          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>Session</span>
            <strong>
              {session.status === "authenticated" && currentSession
                ? currentSession.username
                : session.status === "bootstrapping"
                  ? "bootstrapping"
                  : "anonymous"}
            </strong>
            {session.status === "authenticated" && currentSession ? (
              <Button kind="ghost" onClick={session.logout} disabled={session.pending}>
                Выйти
              </Button>
            ) : null}
          </div>
        </aside>
      </section>

      {session.status === "authenticated" && currentSession ? (
        <EnrollmentWorkspace session={currentSession} />
      ) : (
        <LoginPanel
          isBootstrapping={session.status === "bootstrapping"}
          isSubmitting={session.pending}
          error={session.error}
          onSubmit={session.login}
        />
      )}
    </main>
  );
}
