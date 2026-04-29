import { QRCodeSVG } from "qrcode.react";
import type { AdminDeviceOnboardingPlatform, AdminTenantDirectoryDetailView } from "../../shared/types/admin-contracts";
import { formatUtcInstant } from "../../shared/time/formatUtcInstant";
import { Button } from "../../shared/ui/Button";
import { UserDeviceStatusBadge } from "../user-devices/UserDeviceStatusBadge";
import type { TenantManagementWorkspaceState } from "./model/useTenantManagementWorkspace";
import styles from "./TenantManagementWorkspace.module.css";

interface TenantManagementUsersDevicesTabProps {
  directory: AdminTenantDirectoryDetailView;
  workspace: TenantManagementWorkspaceState;
}

const platforms: AdminDeviceOnboardingPlatform[] = ["android", "ios", "unknown"];

export function TenantManagementUsersDevicesTab({ directory, workspace }: TenantManagementUsersDevicesTabProps) {
  return (
    <div className={styles.grid} role="tabpanel">
      <section className={styles.section}>
        <h3>Selected user</h3>
        <div className={styles.form}>
          <label className={styles.field}>
            <span>External User ID</span>
            <input
              value={workspace.userDraft.externalUserId}
              onChange={(event) => workspace.setUserDraft((current) => ({ ...current, externalUserId: event.target.value }))}
              placeholder="keycloak-sub-or-user-id"
            />
          </label>

          <label className={styles.field}>
            <span>Application</span>
            <select
              value={workspace.userDraft.applicationClientId}
              onChange={(event) => workspace.setUserDraft((current) => ({ ...current, applicationClientId: event.target.value }))}
            >
              {directory.applications.map((application) => (
                <option key={application.applicationClientId} value={application.applicationClientId}>
                  {application.displayName} / {application.applicationClientId}
                </option>
              ))}
            </select>
          </label>

          <div className={styles.actions}>
            <Button onClick={() => void workspace.loadDevices()} disabled={!workspace.canReadDevices || workspace.pendingAction === "loadDevices"}>
              {workspace.pendingAction === "loadDevices" ? "Loading..." : "Load user devices"}
            </Button>
          </div>
        </div>
      </section>

      <section className={styles.section}>
        <h3>Devices</h3>
        {workspace.devices.length === 0 ? (
          <p className={styles.empty}>No devices loaded for the selected user.</p>
        ) : (
          <div className={styles.stack}>
            {workspace.devices.map((device) => (
              <div key={device.deviceId} className={styles.selectRow}>
                <article className={styles.row}>
                  <strong>{device.deviceId}</strong>
                  <UserDeviceStatusBadge status={device.status} />
                  <span>{device.platform} / push {device.isPushCapable ? "enabled" : "disabled"}</span>
                </article>
                <Button kind="secondary" onClick={() => workspace.setSelectedDeviceId(device.deviceId)}>
                  {workspace.selectedDeviceId === device.deviceId ? "Selected" : "Select"}
                </Button>
              </div>
            ))}
          </div>
        )}

        {workspace.selectedDevice ? (
          <div className={styles.form}>
            <dl className={styles.meta}>
              <div>
                <dt>Activated</dt>
                <dd>{formatUtcInstant(workspace.selectedDevice.activatedAtUtc)}</dd>
              </div>
              <div>
                <dt>Last seen</dt>
                <dd>{formatUtcInstant(workspace.selectedDevice.lastSeenAtUtc)}</dd>
              </div>
            </dl>
            <label className={styles.confirmation}>
              <input
                type="checkbox"
                checked={workspace.deviceRevokeArmed}
                disabled={!workspace.canWriteDevices || workspace.selectedDevice.status !== "active"}
                onChange={(event) => workspace.setDeviceRevokeArmed(event.target.checked)}
              />
              <span>Revoke this active device for the loaded user.</span>
            </label>
            <Button
              kind="danger"
              onClick={() => void workspace.revokeDevice()}
              disabled={!workspace.deviceRevokeArmed || workspace.selectedDevice.status !== "active" || workspace.pendingAction === "revokeDevice"}
              stretch
            >
              Revoke selected device
            </Button>
          </div>
        ) : null}
      </section>

      <section className={styles.section}>
        <h3>Issue QR onboarding</h3>
        <div className={styles.form}>
          <label className={styles.field}>
            <span>Platform</span>
            <select
              value={workspace.userDraft.platform}
              onChange={(event) => workspace.setUserDraft((current) => ({ ...current, platform: event.target.value as AdminDeviceOnboardingPlatform }))}
            >
              {platforms.map((platform) => <option key={platform} value={platform}>{platform}</option>)}
            </select>
          </label>

          <label className={styles.field}>
            <span>TTL minutes</span>
            <input
              value={workspace.userDraft.ttlMinutes}
              inputMode="numeric"
              onChange={(event) => workspace.setUserDraft((current) => ({ ...current, ttlMinutes: event.target.value }))}
            />
          </label>

          <Button onClick={() => void workspace.createQrArtifact()} disabled={!workspace.canWriteCombinedOnboarding || workspace.pendingAction === "createQr"} stretch>
            {workspace.pendingAction === "createQr" ? "Issuing..." : "Issue combined QR for selected user"}
          </Button>
        </div>

        {workspace.oneTimeQrPayload ? (
          <div className={styles.secretBox} aria-live="polite">
            <strong>One-time combined onboarding QR</strong>
            <QRCodeSVG
              value={workspace.oneTimeQrPayload.qrEnvelopeValue}
              size={164}
              level="M"
              title="One-time combined onboarding QR"
              role="img"
              aria-label="One-time combined onboarding QR"
            />
            <span>Runtime: {workspace.oneTimeQrPayload.runtimeBaseUrl}</span>
            <span>Expires: {formatUtcInstant(workspace.oneTimeQrPayload.expiresAtUtc)}</span>
            <span>Activation: {workspace.oneTimeQrPayload.activationCodeId}</span>
            {workspace.oneTimeQrPayload.totpEnrollmentId ? (
              <span>TOTP enrollment: {workspace.oneTimeQrPayload.totpEnrollmentId}</span>
            ) : null}
            <div className={styles.actions}>
              <Button kind="secondary" onClick={() => void workspace.copyQrPayload()} disabled={workspace.pendingAction === "copy"}>
                Copy QR payload
              </Button>
              <Button kind="danger" onClick={workspace.discardSecrets}>Discard payload</Button>
            </div>
          </div>
        ) : null}
      </section>
    </div>
  );
}
