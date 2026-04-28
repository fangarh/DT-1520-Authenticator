import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import type { AdminIntegrationClientScope } from "../../shared/types/admin-contracts";
import { integrationClientScopeOptions } from "../../shared/types/integration-client-scopes";
import type { IntegrationClientPendingAction } from "./model/integrationClientWorkspaceModel";
import styles from "./IntegrationClientCreatePanel.module.css";

interface OneTimeSecret {
  clientId: string;
  clientSecret: string;
}

interface IntegrationClientCreatePanelProps {
  tenantId: string;
  applicationClientId: string;
  clientId: string;
  allowedScopes: AdminIntegrationClientScope[];
  oneTimeSecret: OneTimeSecret | null;
  pending: IntegrationClientPendingAction;
  canWrite: boolean;
  onTenantIdChange: (value: string) => void;
  onApplicationClientIdChange: (value: string) => void;
  onClientIdChange: (value: string) => void;
  onToggleScope: (scope: AdminIntegrationClientScope) => void;
  onCreate: () => Promise<void>;
  onCopySecret: () => Promise<void>;
  onDiscardSecret: () => void;
  onReset: () => void;
}

export function IntegrationClientCreatePanel(props: IntegrationClientCreatePanelProps) {
  return (
    <Panel
      eyebrow="Client Onboarding"
      title="Create integration client"
      aside={<span className={styles.scope}>{props.canWrite ? "write enabled" : "write missing"}</span>}
    >
      <div className={styles.form}>
        <label className={styles.field}>
          <span>Tenant ID</span>
          <input
            value={props.tenantId}
            onChange={(event) => props.onTenantIdChange(event.target.value)}
            placeholder="6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"
          />
        </label>

        <label className={styles.field}>
          <span>Application Client ID</span>
          <input
            value={props.applicationClientId}
            onChange={(event) => props.onApplicationClientIdChange(event.target.value)}
            placeholder="f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"
          />
        </label>

        <label className={styles.field}>
          <span>Client ID</span>
          <input
            value={props.clientId}
            onChange={(event) => props.onClientIdChange(event.target.value)}
            placeholder="project-manager-prod"
          />
        </label>

        <div className={styles.scopeGroup}>
          <span className={styles.scopeLabel}>Allowed scopes</span>
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
          <Button onClick={() => void props.onCreate()} disabled={props.pending === "create" || !props.canWrite} stretch>
            {props.pending === "create" ? "Creating..." : "Create client"}
          </Button>
          <Button kind="secondary" onClick={props.onReset} disabled={props.pending === "create"}>
            Clear form
          </Button>
        </div>

        {props.oneTimeSecret ? (
          <div className={styles.secretPanel} aria-live="polite">
            <div>
              <strong>One-time secret for {props.oneTimeSecret.clientId}</strong>
              <p>Backend will not return this value again after this response.</p>
            </div>
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
