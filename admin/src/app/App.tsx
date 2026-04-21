import { LoginPanel } from "../features/auth/LoginPanel";
import { DeliveryStatusWorkspace } from "../features/delivery-statuses/DeliveryStatusWorkspace";
import { useAdminSession } from "../features/auth/useAdminSession";
import { EnrollmentWorkspace } from "../features/enrollment-workspace/EnrollmentWorkspace";
import { UserDeviceWorkspace } from "../features/user-devices/UserDeviceWorkspace";
import { WebhookSubscriptionWorkspace } from "../features/webhook-subscriptions/WebhookSubscriptionWorkspace";
import { Button } from "../shared/ui/Button";
import { Notice } from "../shared/ui/Notice";
import styles from "./App.module.css";

export default function App() {
  const session = useAdminSession();
  const currentSession = session.status === "authenticated" ? session.current : null;
  const canManageEnrollments = currentSession?.permissions.some((permission) => permission.startsWith("enrollments.")) ?? false;
  const canManageDevices = currentSession?.permissions.some((permission) => permission.startsWith("devices.")) ?? false;
  const canManageWebhooks = currentSession?.permissions.some((permission) => permission.startsWith("webhooks.")) ?? false;
  const canReadDeliveryStatuses = currentSession?.permissions.includes("webhooks.read") ?? false;

  return (
    <main className={styles.shell}>
      <section className={styles.chrome}>
        <div className={styles.hero}>
          <p className={styles.eyebrow}>OTPAuth Admin</p>
          <h1 className={styles.title}>Operator console for enrollment and delivery visibility.</h1>
          <p className={styles.subtitle}>
            Browser contour now talks only to `/api/v1/admin/*`: session cookie,
            CSRF, enrollment lifecycle, user device support, recent delivery outcomes and webhook subscription management.
          </p>
        </div>

        <aside className={styles.sidebar}>
          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>Contour</span>
            <strong>admin cookie session</strong>
            <span className={styles.metaHint}>
              {currentSession ? currentSession.permissions.join(" / ") : "anonymous"}
            </span>
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
        <div className={styles.workspaceStack}>
          {canManageEnrollments ? <EnrollmentWorkspace session={currentSession} /> : null}
          {canManageDevices ? <UserDeviceWorkspace session={currentSession} /> : null}
          {canReadDeliveryStatuses ? <DeliveryStatusWorkspace session={currentSession} /> : null}
          {canManageWebhooks ? <WebhookSubscriptionWorkspace session={currentSession} /> : null}
          {!canManageEnrollments && !canManageDevices && !canManageWebhooks && !canReadDeliveryStatuses ? (
            <Notice
              tone="neutral"
              title="Нет доступных operator surfaces"
              detail="Текущая сессия не содержит permissions для enrollment, device или delivery/webhook management."
            />
          ) : null}
        </div>
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
