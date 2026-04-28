import type { AdminTenantDirectoryDetailView } from "../../shared/types/admin-contracts";
import { Button } from "../../shared/ui/Button";
import { resolveDeviceOnboardingRuntimeBaseUrl } from "../device-onboarding/model/deviceOnboardingQrEnvelope";
import type { TenantManagementWorkspaceState } from "./model/useTenantManagementWorkspace";
import styles from "./TenantManagementWorkspace.module.css";

interface TenantManagementRuntimeTabProps {
  directory: AdminTenantDirectoryDetailView;
  workspace: TenantManagementWorkspaceState;
}

export function TenantManagementRuntimeTab({ directory, workspace }: TenantManagementRuntimeTabProps) {
  return (
    <div className={styles.stack} role="tabpanel">
      <section className={styles.section}>
        <h3>Runtime and callback policy</h3>
        <div className={styles.actions}>
          <Button onClick={() => void workspace.loadRuntimeConfiguration()} disabled={workspace.pendingAction === "loadRuntime"}>
            {workspace.pendingAction === "loadRuntime" ? "Loading..." : "Load runtime configuration"}
          </Button>
        </div>

        <dl className={styles.meta}>
          <div>
            <dt>Tenant</dt>
            <dd>{directory.tenant.tenantId}</dd>
          </div>
          <div>
            <dt>QR runtime base URL</dt>
            <dd>{resolveDeviceOnboardingRuntimeBaseUrl()}</dd>
          </div>
          <div>
            <dt>Callback policy</dt>
            <dd>{workspace.runtimeConfiguration?.callbackUrlPolicy.mode ?? "not loaded"}</dd>
          </div>
          <div>
            <dt>HTTP relaxation</dt>
            <dd>{workspace.runtimeConfiguration?.callbackUrlPolicy.allowInsecureHttp ? "enabled" : "disabled"}</dd>
          </div>
        </dl>

        <p className={styles.empty}>
          Runtime configuration read model exposes policy metadata only. It does not return callback URLs, signing secrets or raw payloads.
        </p>
      </section>

      <section className={styles.section}>
        <h3>Callback routing warnings</h3>
        <div className={styles.stack}>
          {workspace.runtimeConfiguration?.callbackUrlPolicy.mode === "PublicInternet" ? (
            <p className={styles.warning}>PublicInternet requires HTTPS public callbacks and rejects localhost/private IP literals.</p>
          ) : null}
          {workspace.runtimeConfiguration?.callbackUrlPolicy.mode === "PrivateNetwork" ? (
            <p className={styles.warning}>PrivateNetwork is opt-in for closed contours. Operators should keep HTTP relaxation disabled unless explicitly needed.</p>
          ) : null}
          {workspace.runtimeConfiguration?.callbackUrlPolicy.mode === "LocalDevelopment" ? (
            <p className={styles.warning}>LocalDevelopment is for local/demo use only and must not be production default.</p>
          ) : null}
          {!workspace.runtimeConfiguration ? (
            <p className={styles.empty}>Load runtime configuration to show active callback policy warnings.</p>
          ) : null}
        </div>
      </section>
    </div>
  );
}
