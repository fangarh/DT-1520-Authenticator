import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import type { InstallerShellInfo } from "../../shared/types/installer-contracts";
import { getInstallerModeGuidance } from "./model/installer-mode-guidance";
import {
  getInstallerModeLabel,
  installerPermissionOptions,
  shouldRequestBootstrapPassword,
  type InstallerRunDraft,
} from "./model/installer-run-draft";
import {
  getInstallerDraftGuardrails,
  hasBlockingInstallerDraftGuardrails,
} from "./model/installer-run-guardrails";
import styles from "./InstallerRunForm.module.css";

interface InstallerRunFormProps {
  draft: InstallerRunDraft;
  shellInfo: InstallerShellInfo | null;
  pending: boolean;
  loadingShellInfo: boolean;
  onDraftChange: (updater: (current: InstallerRunDraft) => InstallerRunDraft) => void;
  onPermissionEnabled: (permission: (typeof installerPermissionOptions)[number], enabled: boolean) => void;
  onSubmit: () => void;
}

export function InstallerRunForm({
  draft,
  shellInfo,
  pending,
  loadingShellInfo,
  onDraftChange,
  onPermissionEnabled,
  onSubmit,
}: InstallerRunFormProps) {
  const requiresBootstrapPassword = shouldRequestBootstrapPassword(draft);
  const showImageBuildToggle = draft.mode !== "Recover";
  const showBootstrapToggle = draft.mode !== "Recover";
  const showBootstrapAdminToggle = draft.mode === "Install";
  const modeGuidance = getInstallerModeGuidance(draft.mode);
  const guardrails = getInstallerDraftGuardrails(draft);
  const hasBlockingGuardrails = hasBlockingInstallerDraftGuardrails(guardrails);
  const envFileInputId = "installer-env-file-path";
  const composeFileInputId = "installer-compose-file-path";
  const adminUsernameInputId = "installer-bootstrap-admin-username";
  const adminPasswordInputId = "installer-bootstrap-admin-password";
  const submitLabel = draft.preflightOnly
    ? "Run preflight checks"
    : draft.dryRun
      ? `Generate ${draft.mode.toLowerCase()} dry-run plan`
      : `Run ${draft.mode.toLowerCase()} flow`;

  return (
    <Panel
      title="Local setup shell"
      eyebrow="Iteration 4"
      aside={<span className={styles.transportTag}>{shellInfo ? shellInfo.executionMode : "loading"}</span>}
    >
      <div className={styles.layout}>
        <section className={styles.fieldset}>
          <span className={styles.legend}>Mode</span>
          <div className={styles.modeRow}>
            {(["Install", "Update", "Recover"] as const).map((mode) => (
              <label key={mode} className={[styles.modeOption, draft.mode === mode ? styles.modeOptionSelected : ""].join(" ")}>
                <input
                  checked={draft.mode === mode}
                  name="mode"
                  type="radio"
                  value={mode}
                  onChange={() => onDraftChange((current) => ({ ...current, mode }))}
                />
                <span>{getInstallerModeLabel(mode)}</span>
              </label>
            ))}
          </div>

          <article className={styles.modeNarrative}>
            <div className={styles.modeNarrativeHeader}>
              <strong>{modeGuidance.label} flow</strong>
              <small>{shellInfo?.executionMode === "mock" ? "mock bridge" : "live bridge"}</small>
            </div>
            <p>{modeGuidance.description}</p>
            <ul className={styles.modeFactList}>
              <li>{modeGuidance.semantics}</li>
              <li>{modeGuidance.completion}</li>
            </ul>
          </article>
        </section>

        <div className={styles.fieldGrid}>
          <label className={styles.field}>
            <span>Env file path</span>
            <input
              aria-label="Env file path"
              id={envFileInputId}
              placeholder="C:\\secure\\otpauth\\runtime.env"
              type="text"
              value={draft.envFilePath}
              onChange={(event) => onDraftChange((current) => ({ ...current, envFilePath: event.target.value }))}
            />
            <small>Host-level env file outside the repository. The engine will fail-closed if the path points back into git.</small>
          </label>

          <label className={styles.field}>
            <span>Compose file path</span>
            <input
              aria-label="Compose file path"
              id={composeFileInputId}
              placeholder={shellInfo?.defaultComposeFilePath ?? "Optional; installer will use infra/docker-compose.yml by default"}
              type="text"
              value={draft.composeFilePath}
              onChange={(event) => onDraftChange((current) => ({ ...current, composeFilePath: event.target.value }))}
            />
            <small>Optional override for the compose contract. Empty means use the canonical `infra/docker-compose.yml`.</small>
          </label>
        </div>

        <div className={styles.fieldGrid}>
          <label className={styles.field}>
            <span>Bootstrap admin username</span>
            <input
              aria-label="Bootstrap admin username"
              id={adminUsernameInputId}
              type="text"
              value={draft.bootstrapAdminUsername}
              onChange={(event) => onDraftChange((current) => ({ ...current, bootstrapAdminUsername: event.target.value }))}
            />
          </label>

          {requiresBootstrapPassword ? (
            <label className={styles.field}>
              <span>Bootstrap admin password</span>
              <input
                autoComplete="new-password"
                aria-label="Bootstrap admin password"
                id={adminPasswordInputId}
                type="password"
                value={draft.bootstrapAdminPassword}
                onChange={(event) => onDraftChange((current) => ({ ...current, bootstrapAdminPassword: event.target.value }))}
              />
              <small>Password is passed only through process-level `OTPAUTH_ADMIN_PASSWORD` and cleared from UI state after the run.</small>
            </label>
          ) : (
            <div className={[styles.field, styles.fieldHint].join(" ")}>
              <span>Password handling</span>
              <small>
                This mode does not need bootstrap admin credentials unless you explicitly bring admin creation back into the engine path.
              </small>
            </div>
          )}
        </div>

        <section className={styles.fieldset}>
          <span className={styles.legend}>Bootstrap permissions</span>
          <div className={styles.checkboxRow}>
            {installerPermissionOptions.map((permission) => (
              <label key={permission} className={styles.checkboxLabel}>
                <input
                  checked={draft.bootstrapAdminPermissions.includes(permission)}
                  type="checkbox"
                  onChange={(event) => onPermissionEnabled(permission, event.target.checked)}
                />
                <span>{permission}</span>
              </label>
            ))}
          </div>
        </section>

        <section className={styles.fieldset}>
          <span className={styles.legend}>Execution options</span>
          <div className={styles.checkboxGrid}>
            <label className={styles.checkboxLabel}>
              <input
                checked={draft.preflightOnly}
                type="checkbox"
                onChange={(event) => onDraftChange((current) => ({ ...current, preflightOnly: event.target.checked }))}
              />
              <span>Preflight only</span>
            </label>

            <label className={styles.checkboxLabel}>
              <input
                checked={draft.dryRun}
                type="checkbox"
                onChange={(event) => onDraftChange((current) => ({ ...current, dryRun: event.target.checked }))}
              />
              <span>Dry run</span>
            </label>

            <label className={styles.checkboxLabel}>
              <input
                checked={draft.skipPortAvailabilityCheck}
                type="checkbox"
                onChange={(event) => onDraftChange((current) => ({ ...current, skipPortAvailabilityCheck: event.target.checked }))}
              />
              <span>Skip admin port probe</span>
            </label>

            {showImageBuildToggle ? (
              <label className={styles.checkboxLabel}>
                <input
                  checked={draft.skipImageBuild}
                  type="checkbox"
                  onChange={(event) => onDraftChange((current) => ({ ...current, skipImageBuild: event.target.checked }))}
                />
                <span>Skip image build</span>
              </label>
            ) : null}

            {showBootstrapToggle ? (
              <label className={styles.checkboxLabel}>
                <input
                  checked={draft.skipBootstrap}
                  type="checkbox"
                  onChange={(event) => onDraftChange((current) => ({ ...current, skipBootstrap: event.target.checked }))}
                />
                <span>Skip bootstrap commands</span>
              </label>
            ) : null}

            {showBootstrapAdminToggle ? (
              <label className={styles.checkboxLabel}>
                <input
                  checked={draft.skipBootstrapAdmin}
                  type="checkbox"
                  onChange={(event) => onDraftChange((current) => ({ ...current, skipBootstrapAdmin: event.target.checked }))}
                />
                <span>Skip bootstrap admin upsert</span>
              </label>
            ) : null}
          </div>
        </section>

        {guardrails.length > 0 ? (
          <section className={styles.fieldset}>
            <span className={styles.legend}>Guardrails</span>
            <div className={styles.guardrailList}>
              {guardrails.map((guardrail) => (
                <article key={`${guardrail.title}-${guardrail.detail}`} className={[styles.guardrailCard, styles[guardrail.tone]].join(" ")}>
                  <strong>{guardrail.title}</strong>
                  <small>{guardrail.detail}</small>
                </article>
              ))}
            </div>
          </section>
        ) : null}

        <div className={styles.footer}>
          <div className={styles.footerCopy}>
            <strong>Loopback-only shell</strong>
            <small>
              The browser never talks to runtime `Admin UI`. It only sends local input to a separate bridge, which then invokes the script
              engine.
            </small>
          </div>

          <Button stretch disabled={pending || loadingShellInfo || hasBlockingGuardrails} onClick={onSubmit}>
            {pending ? "Running installer engine..." : submitLabel}
          </Button>
        </div>
      </div>
    </Panel>
  );
}
