import type { AdminIntegrationClientScope, AdminTenantDirectoryDetailView } from "../../shared/types/admin-contracts";
import { integrationClientScopeOptions } from "../../shared/types/integration-client-scopes";
import { Button } from "../../shared/ui/Button";
import { IntegrationClientStatusBadge } from "../integration-clients/IntegrationClientStatusBadge";
import type { TenantManagementWorkspaceState } from "./model/useTenantManagementWorkspace";
import styles from "./TenantManagementWorkspace.module.css";

interface TenantManagementApiClientsTabProps {
  directory: AdminTenantDirectoryDetailView;
  workspace: TenantManagementWorkspaceState;
}

export function TenantManagementApiClientsTab({ directory, workspace }: TenantManagementApiClientsTabProps) {
  const selectedClient = workspace.selectedClient;
  const isActive = selectedClient?.status === "active";
  const isInactive = selectedClient?.status === "inactive";

  return (
    <div className={styles.grid} role="tabpanel">
      <section className={styles.section}>
        <h3>Tenant API clients</h3>
        <div className={styles.actions}>
          <Button onClick={() => void workspace.loadClients()} disabled={!workspace.canReadClients || workspace.pendingAction === "loadClients"}>
            {workspace.pendingAction === "loadClients" ? "Loading..." : "Refresh clients"}
          </Button>
        </div>

        {workspace.clients.length === 0 ? (
          <p className={styles.empty}>No API clients for selected tenant.</p>
        ) : (
          <div className={styles.stack}>
            {workspace.clients.map((client) => (
              <div key={client.clientId} className={styles.selectRow}>
                <article className={styles.row}>
                  <strong>{client.clientId}</strong>
                  <code>{client.applicationClientId}</code>
                  <span>{client.allowedScopes.join(", ")}</span>
                </article>
                <Button kind="secondary" onClick={() => workspace.selectClient(client)}>
                  {workspace.selectedClientId === client.clientId ? "Selected" : "Select"}
                </Button>
              </div>
            ))}
          </div>
        )}
      </section>

      <section className={styles.section}>
        <h3>Create client</h3>
        <div className={styles.form}>
          <label className={styles.field}>
            <span>Application</span>
            <select
              value={workspace.clientDraft.applicationClientId}
              onChange={(event) => workspace.setClientDraft((current) => ({ ...current, applicationClientId: event.target.value }))}
            >
              {directory.applications.map((application) => (
                <option key={application.applicationClientId} value={application.applicationClientId}>
                  {application.displayName} / {application.applicationClientId}
                </option>
              ))}
            </select>
          </label>

          <label className={styles.field}>
            <span>Client ID</span>
            <input
              value={workspace.clientDraft.clientId}
              onChange={(event) => workspace.setClientDraft((current) => ({ ...current, clientId: event.target.value }))}
              placeholder="project-manager-prod"
            />
          </label>

          <ScopeCheckboxes
            scopes={workspace.clientDraft.allowedScopes}
            disabled={!workspace.canWriteClients || workspace.pendingAction === "createClient"}
            onToggle={workspace.toggleClientCreateScope}
          />

          <Button onClick={() => void workspace.createClient()} disabled={!workspace.canWriteClients || workspace.pendingAction === "createClient"} stretch>
            {workspace.pendingAction === "createClient" ? "Creating..." : "Create client in selected tenant"}
          </Button>

          {workspace.oneTimeSecret ? (
            <OneTimeSecretBox
              label={`One-time secret for ${workspace.oneTimeSecret.clientId}`}
              value={workspace.oneTimeSecret.clientSecret}
              pending={workspace.pendingAction === "copy"}
              onCopy={workspace.copyOneTimeSecret}
              onDiscard={workspace.discardSecrets}
            />
          ) : null}
        </div>
      </section>

      <section className={styles.section}>
        <h3>Selected client lifecycle</h3>
        {!selectedClient ? (
          <p className={styles.empty}>Select an API client before lifecycle actions.</p>
        ) : (
          <div className={styles.form}>
            <article className={styles.row}>
              <strong>{selectedClient.clientId}</strong>
              <IntegrationClientStatusBadge status={selectedClient.status} />
              <span>Commands are bound to tenant {workspace.tenantId}.</span>
            </article>

            <ScopeCheckboxes
              scopes={workspace.scopeDraft}
              disabled={!workspace.canWriteClients || workspace.pendingAction === "scopes"}
              onToggle={workspace.toggleLifecycleScope}
            />
            <Button
              onClick={() => void workspace.updateScopes()}
              disabled={!workspace.canWriteClients || !workspace.hasScopeChanges || workspace.scopeDraft.length === 0 || workspace.pendingAction === "scopes"}
              stretch
            >
              {workspace.pendingAction === "scopes" ? "Saving..." : "Save scopes"}
            </Button>

            <label className={styles.confirmation}>
              <input
                type="checkbox"
                checked={workspace.rotateArmed}
                disabled={!workspace.canWriteClients || !isActive || workspace.pendingAction === "rotate"}
                onChange={(event) => workspace.setRotateArmed(event.target.checked)}
              />
              <span>Rotate client secret and invalidate the old secret.</span>
            </label>
            <Button kind="danger" onClick={() => void workspace.rotateSecret()} disabled={!workspace.rotateArmed || !isActive || workspace.pendingAction === "rotate"} stretch>
              {workspace.pendingAction === "rotate" ? "Rotating..." : "Rotate secret"}
            </Button>

            {workspace.rotatedSecret ? (
              <OneTimeSecretBox
                label={`Rotated secret for ${workspace.rotatedSecret.clientId}`}
                value={workspace.rotatedSecret.clientSecret}
                pending={workspace.pendingAction === "copy"}
                onCopy={workspace.copyRotatedSecret}
                onDiscard={workspace.discardSecrets}
              />
            ) : null}

            <label className={styles.confirmation}>
              <input
                type="checkbox"
                checked={workspace.deactivateArmed}
                disabled={!workspace.canWriteClients || !isActive}
                onChange={(event) => workspace.setDeactivateArmed(event.target.checked)}
              />
              <span>Deactivate this client and invalidate issued tokens.</span>
            </label>
            <Button kind="danger" onClick={() => void workspace.deactivateClient()} disabled={!workspace.deactivateArmed || !isActive || workspace.pendingAction === "deactivate"} stretch>
              Deactivate client
            </Button>

            <label className={styles.confirmation}>
              <input
                type="checkbox"
                checked={workspace.reactivateArmed}
                disabled={!workspace.canWriteClients || !isInactive}
                onChange={(event) => workspace.setReactivateArmed(event.target.checked)}
              />
              <span>Reactivate this inactive client.</span>
            </label>
            <Button kind="secondary" onClick={() => void workspace.reactivateClient()} disabled={!workspace.reactivateArmed || !isInactive || workspace.pendingAction === "reactivate"} stretch>
              Reactivate client
            </Button>
          </div>
        )}
      </section>
    </div>
  );
}

function ScopeCheckboxes(props: {
  scopes: AdminIntegrationClientScope[];
  disabled: boolean;
  onToggle: (scope: AdminIntegrationClientScope) => void;
}) {
  return (
    <div className={styles.scopeList}>
      {integrationClientScopeOptions.map((scope) => (
        <label key={scope.value} className={styles.scopeItem}>
          <input
            type="checkbox"
            checked={props.scopes.includes(scope.value)}
            disabled={props.disabled}
            onChange={() => props.onToggle(scope.value)}
          />
          <span>
            {scope.label}
            <small>{scope.description}</small>
          </span>
        </label>
      ))}
    </div>
  );
}

function OneTimeSecretBox(props: {
  label: string;
  value: string;
  pending: boolean;
  onCopy: () => Promise<void>;
  onDiscard: () => void;
}) {
  return (
    <div className={styles.secretBox} aria-live="polite">
      <strong>{props.label}</strong>
      <code className={styles.secretValue}>{props.value}</code>
      <div className={styles.actions}>
        <Button kind="secondary" onClick={() => void props.onCopy()} disabled={props.pending}>
          {props.pending ? "Copying..." : "Copy secret"}
        </Button>
        <Button kind="danger" onClick={props.onDiscard}>Discard secret</Button>
      </div>
    </div>
  );
}
