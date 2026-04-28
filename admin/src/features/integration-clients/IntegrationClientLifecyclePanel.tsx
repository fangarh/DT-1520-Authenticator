import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import type { AdminIntegrationClientScope, AdminIntegrationClientView } from "../../shared/types/admin-contracts";
import { integrationClientScopeOptions } from "../../shared/types/integration-client-scopes";
import { IntegrationClientStatusBadge } from "./IntegrationClientStatusBadge";
import type { IntegrationClientPendingAction, OneTimeSecret } from "./model/integrationClientWorkspaceModel";
import styles from "./IntegrationClientLifecyclePanel.module.css";

interface IntegrationClientLifecyclePanelProps {
  client: AdminIntegrationClientView | null;
  scopeDraft: AdminIntegrationClientScope[];
  hasScopeChanges: boolean;
  rotatedSecret: OneTimeSecret | null;
  pending: IntegrationClientPendingAction;
  canWrite: boolean;
  rotateArmed: boolean;
  deactivateArmed: boolean;
  reactivateArmed: boolean;
  onToggleScope: (scope: AdminIntegrationClientScope) => void;
  onUpdateScopes: () => Promise<void>;
  onRotateSecret: () => Promise<void>;
  onDeactivate: () => Promise<void>;
  onReactivate: () => Promise<void>;
  onCopyRotatedSecret: () => Promise<void>;
  onDiscardRotatedSecret: () => void;
  onRotateArmedChange: (armed: boolean) => void;
  onDeactivateArmedChange: (armed: boolean) => void;
  onReactivateArmedChange: (armed: boolean) => void;
}

export function IntegrationClientLifecyclePanel(props: IntegrationClientLifecyclePanelProps) {
  const isActive = props.client?.status === "active";
  const isInactive = props.client?.status === "inactive";
  const isBusy = props.pending !== null;

  return (
    <Panel
      eyebrow="Client Lifecycle"
      title="Manage selected client"
      aside={props.client ? <IntegrationClientStatusBadge status={props.client.status} /> : undefined}
    >
      {!props.client ? (
        <p className={styles.empty}>Выберите integration client из списка, чтобы открыть lifecycle actions.</p>
      ) : (
        <div className={styles.layout}>
          <div className={styles.summary}>
            <strong>{props.client.clientId}</strong>
            <p>
              Action scope is bound to <strong>{props.client.tenantId}</strong> / <strong>{props.client.clientId}</strong>.
            </p>
            {isInactive ? (
              <p className={styles.warning}>Inactive clients cannot receive usable tokens until reactivation.</p>
            ) : null}
            {!props.canWrite ? (
              <p className={styles.warning}>Permission `integration-clients.write` is required for lifecycle actions.</p>
            ) : null}
          </div>

          <div className={styles.actionCard}>
            <strong>Allowed scopes</strong>
            <div className={styles.scopeList}>
              {integrationClientScopeOptions.map((scopeOption) => (
                <label key={scopeOption.value} className={styles.scopeItem}>
                  <input
                    type="checkbox"
                    checked={props.scopeDraft.includes(scopeOption.value)}
                    disabled={!props.canWrite || props.pending === "scopes"}
                    onChange={() => props.onToggleScope(scopeOption.value)}
                  />
                  <span>{scopeOption.label}</span>
                  <small>{scopeOption.description}</small>
                </label>
              ))}
            </div>
            <Button
              onClick={() => void props.onUpdateScopes()}
              disabled={!props.canWrite || !props.hasScopeChanges || props.scopeDraft.length === 0 || props.pending === "scopes"}
              stretch
            >
              {props.pending === "scopes" ? "Saving scopes..." : "Save scopes"}
            </Button>
          </div>

          <div className={styles.actionCard}>
            <strong>Rotate secret</strong>
            <label className={styles.confirmation}>
              <input
                type="checkbox"
                checked={props.rotateArmed}
                disabled={!props.canWrite || !isActive || props.pending === "rotate"}
                onChange={(event) => props.onRotateArmedChange(event.target.checked)}
              />
              <span>I understand that the previous client secret will stop working.</span>
            </label>
            <Button
              kind="danger"
              onClick={() => void props.onRotateSecret()}
              disabled={!props.canWrite || !isActive || !props.rotateArmed || props.pending === "rotate"}
              stretch
            >
              {props.pending === "rotate" ? "Rotating..." : "Rotate secret"}
            </Button>
          </div>

          {props.rotatedSecret ? (
            <div className={styles.secretPanel} aria-live="polite">
              <div>
                <strong>One-time rotated secret for {props.rotatedSecret.clientId}</strong>
                <p>Backend will not return this value again after this response.</p>
              </div>
              <code>{props.rotatedSecret.clientSecret}</code>
              <div className={styles.actions}>
                <Button kind="secondary" onClick={() => void props.onCopyRotatedSecret()} disabled={props.pending === "copy"}>
                  {props.pending === "copy" ? "Copying..." : "Copy secret"}
                </Button>
                <Button kind="danger" onClick={props.onDiscardRotatedSecret}>
                  Discard secret
                </Button>
              </div>
            </div>
          ) : null}

          <div className={styles.stateGrid}>
            <div className={styles.actionCard}>
              <strong>Deactivate</strong>
              <label className={styles.confirmation}>
                <input
                  type="checkbox"
                  checked={props.deactivateArmed}
                  disabled={!props.canWrite || !isActive || isBusy}
                  onChange={(event) => props.onDeactivateArmedChange(event.target.checked)}
                />
                <span>I understand this invalidates issued tokens for this client.</span>
              </label>
              <Button
                kind="danger"
                onClick={() => void props.onDeactivate()}
                disabled={!props.canWrite || !isActive || !props.deactivateArmed || props.pending === "deactivate"}
                stretch
              >
                {props.pending === "deactivate" ? "Deactivating..." : "Deactivate client"}
              </Button>
            </div>

            <div className={styles.actionCard}>
              <strong>Reactivate</strong>
              <label className={styles.confirmation}>
                <input
                  type="checkbox"
                  checked={props.reactivateArmed}
                  disabled={!props.canWrite || !isInactive || isBusy}
                  onChange={(event) => props.onReactivateArmedChange(event.target.checked)}
                />
                <span>I understand this client can receive tokens again after reactivation.</span>
              </label>
              <Button
                kind="secondary"
                onClick={() => void props.onReactivate()}
                disabled={!props.canWrite || !isInactive || !props.reactivateArmed || props.pending === "reactivate"}
                stretch
              >
                {props.pending === "reactivate" ? "Reactivating..." : "Reactivate client"}
              </Button>
            </div>
          </div>
        </div>
      )}
    </Panel>
  );
}
