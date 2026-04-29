import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import { integrationClientScopeOptions } from "../../shared/types/integration-client-scopes";
import type { AdminIntegrationClientScope } from "../../shared/types/admin-contracts";
import type { TenantDirectoryPendingAction, TenantOneTimeSecret } from "./model/tenantDirectoryWorkspaceModel";
import styles from "./TenantQuickCreatePanel.module.css";

interface TenantQuickCreatePanelProps {
  tenantDisplayName: string;
  applicationDisplayName: string;
  integrationClientDisplayName: string;
  allowedScopes: AdminIntegrationClientScope[];
  oneTimeSecret: TenantOneTimeSecret | null;
  pending: TenantDirectoryPendingAction;
  canWrite: boolean;
  onTenantDisplayNameChange: (value: string) => void;
  onApplicationDisplayNameChange: (value: string) => void;
  onIntegrationClientDisplayNameChange: (value: string) => void;
  onToggleScope: (scope: AdminIntegrationClientScope) => void;
  onQuickCreate: () => Promise<void>;
  onCopySecret: () => Promise<void>;
  onDiscardSecret: () => void;
  onReset: () => void;
}

export function TenantQuickCreatePanel(props: TenantQuickCreatePanelProps) {
  return (
    <Panel
      eyebrow="Setup"
      title="Quick create tenant"
      aside={<span className={styles.scope}>{props.canWrite ? "write enabled" : "write missing"}</span>}
    >
      <div className={styles.form}>
        <label className={styles.field}>
          <span>Tenant display name</span>
          <input
            value={props.tenantDisplayName}
            onChange={(event) => props.onTenantDisplayNameChange(event.target.value)}
            placeholder="Acme Operations"
          />
        </label>

        <label className={styles.field}>
          <span>Application display name</span>
          <input
            value={props.applicationDisplayName}
            onChange={(event) => props.onApplicationDisplayNameChange(event.target.value)}
            placeholder="Project Manager"
          />
        </label>

        <label className={styles.field}>
          <span>API client display name</span>
          <input
            value={props.integrationClientDisplayName}
            onChange={(event) => props.onIntegrationClientDisplayNameChange(event.target.value)}
            placeholder="Backend API"
          />
        </label>

        <div className={styles.scopeGroup}>
          <span className={styles.scopeLabel}>Initial client scopes</span>
          <div className={styles.scopeList}>
            {integrationClientScopeOptions.map((scopeOption) => (
              <label key={scopeOption.value} className={styles.scopeItem}>
                <input
                  type="checkbox"
                  checked={props.allowedScopes.includes(scopeOption.value)}
                  onChange={() => props.onToggleScope(scopeOption.value)}
                />
                <span>{scopeOption.label}</span>
                <small>{scopeOption.description}</small>
              </label>
            ))}
          </div>
        </div>

        <div className={styles.actions}>
          <Button onClick={() => void props.onQuickCreate()} disabled={props.pending === "quickCreate" || !props.canWrite} stretch>
            {props.pending === "quickCreate" ? "Creating..." : "Quick create"}
          </Button>
          <Button kind="secondary" onClick={props.onReset} disabled={props.pending === "quickCreate"}>
            Clear form
          </Button>
        </div>

        {props.oneTimeSecret ? (
          <div className={styles.secretPanel} aria-live="polite">
            <div>
              <strong>One-time secret for {props.oneTimeSecret.clientId}</strong>
              <p>Backend will not return this value again after this response.</p>
            </div>
            <dl className={styles.secretMeta}>
              <div>
                <dt>Tenant ID</dt>
                <dd>{props.oneTimeSecret.tenantId}</dd>
              </div>
              <div>
                <dt>Application Client ID</dt>
                <dd>{props.oneTimeSecret.applicationClientId}</dd>
              </div>
            </dl>
            <code>{props.oneTimeSecret.clientSecret}</code>
            <div className={styles.actions}>
              <Button kind="secondary" onClick={() => void props.onCopySecret()} disabled={props.pending === "copy"}>
                {props.pending === "copy" ? "Copying..." : "Copy secret"}
              </Button>
              <Button kind="danger" onClick={props.onDiscardSecret}>
                Discard secret
              </Button>
            </div>
          </div>
        ) : null}
      </div>
    </Panel>
  );
}
