import { LoginPanel } from "../features/auth/LoginPanel";
import { useAdminSession } from "../features/auth/useAdminSession";
import { DeliveryStatusWorkspace } from "../features/delivery-statuses/DeliveryStatusWorkspace";
import { DeviceOnboardingWorkspace } from "../features/device-onboarding/DeviceOnboardingWorkspace";
import { EnrollmentWorkspace } from "../features/enrollment-workspace/EnrollmentWorkspace";
import { IntegrationClientWorkspace } from "../features/integration-clients/IntegrationClientWorkspace";
import { TenantDirectoryWorkspace } from "../features/tenant-directory/TenantDirectoryWorkspace";
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
  const canManageIntegrationClients = currentSession?.permissions.some((permission) => permission.startsWith("integration-clients.")) ?? false;
  const canManageTenants = currentSession?.permissions.some((permission) => permission.startsWith("tenants.")) ?? false;
  const canManageWebhooks = currentSession?.permissions.some((permission) => permission.startsWith("webhooks.")) ?? false;
  const canReadDeliveryStatuses = currentSession?.permissions.includes("webhooks.read") ?? false;
  const showLegacySurfaces = !canManageTenants;

  return (
    <main className={styles.shell}>
      <section className={styles.chrome}>
        <div className={styles.hero}>
          <p className={styles.eyebrow}>OTPAuth Admin</p>
          <h1 className={styles.title}>Operator console for enrollment and delivery visibility.</h1>
          <p className={styles.subtitle}>
            Browser contour now talks only to `/api/v1/admin/*`: session cookie,
            CSRF and a tenant-centric workspace for setup, client lifecycle, QR device onboarding, runtime policy and reporting. Legacy copy-paste workspaces remain fallback-only for sessions without tenant permissions.
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
          {canManageTenants ? <TenantDirectoryWorkspace session={currentSession} /> : null}
          {showLegacySurfaces && canManageEnrollments ? <EnrollmentWorkspace session={currentSession} /> : null}
          {showLegacySurfaces && canManageIntegrationClients ? <IntegrationClientWorkspace session={currentSession} /> : null}
          {showLegacySurfaces && canManageDevices ? <DeviceOnboardingWorkspace session={currentSession} /> : null}
          {showLegacySurfaces && canManageDevices ? <UserDeviceWorkspace session={currentSession} /> : null}
          {showLegacySurfaces && canReadDeliveryStatuses ? <DeliveryStatusWorkspace session={currentSession} /> : null}
          {showLegacySurfaces && canManageWebhooks ? <WebhookSubscriptionWorkspace session={currentSession} /> : null}
          {!canManageTenants && !canManageEnrollments && !canManageIntegrationClients && !canManageDevices && !canManageWebhooks && !canReadDeliveryStatuses ? (
            <Notice
              tone="neutral"
              title="Нет доступных operator surfaces"
              detail="Текущая сессия не содержит permissions для tenant, enrollment, integration client, device или delivery/webhook management."
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
