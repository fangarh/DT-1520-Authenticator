# Final Integrated Verification Closure Plan

## Status

In progress. Iteration 1 is implemented in code and covered by `rdb_stand` tests; live redeploy/proof is still pending.

This note captures the continuation after the first server-owned `ReferenceBackend` live proof on `ghostring`.

Current live facts:

- `ReferenceBackend` is public on `https://admin.ghostring.ru:18444/`.
- Device QR activation works through `https://admin.ghostring.ru:18443/`.
- Fresh debug Android activation creates an `Active` push-capable device.
- Desktop/WPF approve and deny through `ReferenceBackend` work.
- Android currently needs app restart or foreground reload to see new pending push items.
- Online `TOTP` fallback returned `409` when attempted against a push-selected challenge; code now creates a separate `Totp` fallback challenge, with live proof pending redeploy.

The remaining closure is not another gateway. It is a focused hardening pass over:

- explicit `TOTP` fallback challenge flow in `ReferenceBackend`;
- combined operator QR for device activation plus `TOTP` enrollment;
- Android foreground refresh without app restart;
- final live proof for push approve, push deny and online `TOTP` fallback.

## Problem Statement

### Push proof limitation

`PushDelivery:Provider=logging` does not deliver a real OS push notification. Android reads pending challenges from:

- initial app load;
- foreground/resume-triggered runtime load.

It does not yet run a foreground polling loop. A newly created challenge can therefore stay invisible until the user restarts or reopens the app.

### TOTP fallback contract gap

`ReferenceBackend` starts a protected operation with preferred factors `[Push, Totp]`.

When a push-capable active device exists, `DT-1520` selects `Push`. Calling `verify-totp` for that challenge returns `409 Conflict` because the challenge factor is not `Totp`.

Fallback must not verify a TOTP code against a push challenge. It needs a separate explicit `Totp` challenge for the same protected operation, or a start mode that requests `Totp` directly.

### Onboarding gap

Device onboarding QR activates only the device runtime session. It does not enroll or import a `TOTP` secret into Android.

For a user to have both push and offline code display after one operator action, the operator QR needs to represent a combined onboarding package:

- one opaque device activation payload;
- one TOTP enrollment/provisioning payload;
- one public runtime URL.

The QR must still avoid trusted tenant/user/application claims.

## Target Flow

### Combined user onboarding

1. Operator selects tenant, application and external user in Admin UI.
2. Admin creates a combined onboarding package.
3. QR envelope contains public `runtimeBaseUrl` plus opaque one-time payloads only.
4. Android scans the QR.
5. Android activates the device through `/api/v1/devices/activate-onboarding`.
6. Android imports or confirms the TOTP enrollment.
7. Android stores the runtime URL in encrypted device session storage.
8. Android shows both pending push approvals and TOTP codes.

### Desktop protected operation

Primary path:

1. Desktop starts operation through `ReferenceBackend`.
2. `ReferenceBackend` creates a push-preferred challenge.
3. Android shows pending approval without app restart.
4. Approve or deny terminal callback updates the reference session.

Fallback path:

1. Desktop user chooses `TOTP` fallback.
2. `ReferenceBackend` creates a separate `Totp` challenge bound to the same reference session.
3. Desktop submits the current Android-generated TOTP code.
4. `ReferenceBackend` verifies the `Totp` challenge.
5. The reference session becomes terminal `Approved` only after successful verification.

## Iterations

### Iteration 0. Contract Preflight

Goal: confirm existing backend/admin/mobile contracts before code changes.

Scope:

- read `ReferenceBackend` operation lifecycle and in-memory store;
- read backend challenge create/verify factor behavior;
- read Admin TOTP enrollment command surfaces;
- read Android provisioning/TOTP import surfaces;
- confirm whether current TOTP enrollment QR is admin-generated, mobile-consumed and one-time enough for combined flow.

DoD:

- no production code changes unless a blocking mismatch is found;
- plan note updated if existing contracts force an iteration split change.

Verification:

- targeted source inspection;
- no broad test run required unless code changes.

Security review:

- confirm no proposed QR envelope trusts client-visible tenant/user/application claims;
- confirm TOTP secret is still only displayed/imported through the established provisioning contract.

### Iteration 1. ReferenceBackend Explicit TOTP Fallback

Goal: fix `409` by creating/verifying a real `Totp` challenge for fallback.

Scope:

- add explicit fallback command in `ReferenceBackend`;
- store both primary push challenge id and fallback TOTP challenge id in the reference session;
- when fallback is requested, create challenge with preferred factor `Totp` only;
- verify TOTP against the fallback challenge id;
- keep TOTP code transient and never persisted;
- keep Desktop/WPF UX able to start fallback from a waiting push session.

Out of scope:

- cancelling the original push challenge unless backend already has a safe cancellation model;
- changing SDK public API unless the current typed client cannot express `Totp`-only create.

Expected files:

- `rdb_stand/src/ReferenceBackend/*`
- `rdb_stand/tests/ReferenceBackend.Tests/*`
- `rdb_stand/src/DesktopWpfTest/*` if UI command shape changes
- `rdb_stand/tests/DesktopWpfTest.Tests/*`

Verification:

- targeted `ReferenceBackend.Tests`;
- targeted `DesktopWpfTest.Tests` if touched;
- `dotnet test .\rdb_stand\ReferenceDesktopBackendStand.slnx --no-build -maxcpucount:1` when practical.

Security review:

- no integration secret in Desktop;
- no TOTP code persisted in WPF settings, logs or reference session state;
- fallback cannot approve a session if the TOTP challenge belongs to another session/correlation.

Iteration 1 result on `2026-04-29`:

- `ReferenceBackend` now keeps the primary push-preferred challenge id and a separate fallback `Totp` challenge id on the reference session.
- `POST /api/reference/operations/{sessionId}/totp` lazily creates a `Totp`-only challenge with the same session correlation and verifies the submitted code against that fallback challenge id, not against the push-selected challenge.
- The original push challenge is not cancelled; the first terminal reference-session state is immutable, so later callbacks or fallback submissions cannot overwrite approved/denied/expired/failed outcomes.
- WPF command shape is unchanged. The demo model now displays fallback challenge request/create timestamps and defaults to least-privilege scope `challenges:read challenges:write`.
- Security review: Desktop still does not hold DT-1520 integration credentials; `TOTP` code remains transient UI input and is not stored in settings, logs or reference session records; fallback status application requires the stored fallback challenge id, so a code cannot approve through an unrelated challenge id.

Verification:

- `dotnet test .\rdb_stand\tests\ReferenceBackend.Tests\ReferenceBackend.Tests.csproj -maxcpucount:1` -> `18/18` passed outside sandbox after sandbox `Access denied` on `lib/artifacts/obj`; after terminal-immutability hardening the full solution rerun included `ReferenceBackend.Tests 20/20`.
- `dotnet test .\rdb_stand\tests\DesktopWpfTest.Tests\DesktopWpfTest.Tests.csproj -maxcpucount:1` -> `11/11` passed after stopping only stale blocking `DesktopWpfTest` PID `49336`.
- `dotnet test .\rdb_stand\ReferenceDesktopBackendStand.slnx -maxcpucount:1` -> `ReferenceBackend.Tests 20/20`, `DesktopWpfTest.Tests 11/11` passed.

Next continuation point:

- Iteration 2: Android foreground pending refresh without app restart.

### Iteration 2. Android Pending Refresh Without Restart

Goal: make pending push visible without app restart in the logging-provider MVP contour.

Scope:

- add foreground polling while the push approvals section/app is active;
- add refresh-on-resume;
- optionally add a manual refresh button if it fits existing UI;
- keep polling interval conservative, initially `3-5s`;
- avoid background service or OS notification work in this iteration.

Out of scope:

- FCM provider wiring;
- background polling when app is not foregrounded.

Expected files:

- `mobile/app/src/main/.../AuthenticatorApp.kt`
- `mobile/app/src/main/.../pushapprovals/*`
- `mobile/app/src/test/.../pushapprovals/*`
- possible `mobile/feature/push-approvals/*` only if UI state needs a new command.

Verification:

- `:app:testDebugUnitTest`;
- `:app:assembleDebug`;
- Android MCP install/start smoke;
- live check that a newly created ReferenceBackend push appears without app restart while app is foregrounded.

Security review:

- polling uses existing device bearer session only;
- access/refresh tokens remain encrypted and are not logged;
- network failures show sanitized copy and do not leak backend problem details.

Iteration 2 result on `2026-04-29`:

- `mobile/app` now refreshes pending push approvals on foreground `ON_RESUME` and then polls while the app remains foregrounded.
- Polling interval is conservative and bounded to `3-5s`; the default is `4s`.
- The implementation is app-foreground only: no background service, no OS notification path and no provider-specific push wiring were introduced.
- Security review: polling reuses the existing encrypted device bearer session path; access tokens, refresh tokens, QR payloads and push tokens are not logged; transport/session failures continue to surface only sanitized copy.

Verification:

- `:app:testDebugUnitTest` passed outside sandbox after `%USERPROFILE%\.gradle` wrapper lock `Access denied` in sandbox.
- `:app:assembleDebug` passed outside sandbox after the same Gradle lock condition.
- Fresh `app-debug.apk` was installed and launched on `emulator-5554` through Android MCP; screenshot confirmed foreground `Ожидающие push-запросы` and `Device onboarding` UI.

Next continuation point:

- Iteration 3: Combined Onboarding Backend/Admin Contract.

### Iteration 3. Combined Onboarding Backend/Admin Contract

Goal: let an operator issue one QR that prepares both push device and TOTP.

Preferred contract:

- Admin creates a combined onboarding package for selected tenant/application/external user.
- Backend creates:
  - a device onboarding artifact;
  - a pending TOTP enrollment or provisioning artifact.
- Admin response returns one-time plaintext payloads only in the create response.
- List/detail paths remain sanitized and do not return raw activation payloads or TOTP secret material.

Open design point:

- Prefer reusing existing TOTP enrollment/provisioning contract. Do not duplicate TOTP secret generation or confirmation logic unless the existing flow cannot support mobile import from a combined QR.

Expected files:

- backend admin application contracts/handlers/stores if a new combined endpoint is needed;
- `backend/OtpAuth.Api/Endpoints/*`;
- admin API client and tenant management QR panel.

Verification:

- targeted backend handler/API tests;
- targeted admin API model/component tests;
- targeted Playwright for QR create/discard/non-persistence.

Security review:

- QR contains opaque payloads and public runtime URL only;
- no trusted tenant/user/application claims in QR;
- one-time payloads cleared from React state on discard/reload/navigation;
- audit remains sanitized and excludes TOTP secret, activation payload, push token and callback signing material.

Iteration 3 result on `2026-04-29`:

- Backend added `POST /api/v1/admin/combined-onboarding-packages` as an admin cookie + CSRF command requiring both `devices.write` and `enrollments.write`.
- The combined command reuses the existing admin `TOTP` enrollment start handler and device onboarding artifact handler; it does not duplicate `TOTP` secret generation or confirmation logic.
- The create response returns a one-time device activation payload and one-time `TOTP` provisioning payload only in the `201 Created` response; existing list/current read models remain sanitized and do not return activation payloads, hashes, `secretUri` or `qrCodePayload`.
- Admin tenant management now issues a v2 combined QR envelope `{ v, runtimeBaseUrl, activationPayload, totpProvisioningPayload }` from selected tenant/application/user context and displays the QR only in current React state.
- Security review: the QR envelope carries no trusted `tenantId`, `applicationClientId` or `externalUserId`; TOTP provisioning material is not rendered as raw text in tenant management; discard/reload/navigation clear current-session material; audit remains on existing sanitized device/TOTP events.

Verification:

- targeted backend admin API tests: `19/19` passed.
- targeted admin unit/component tests: `26/26` passed.
- `admin npm run build` passed.
- Playwright tenant regression against production preview: `admin-tenants.spec.ts` `4/4` passed. The default Vite dev-server path hit an environment `esbuild service is no longer running` overlay, so visual verification used the already-built production preview.

Next continuation point:

- Iteration 4: Android combined QR consume flow.

### Iteration 4. Android Combined QR Consume Flow

Goal: after one scan, Android activates device and imports/confirms TOTP.

Scope:

- extend QR payload parser to accept combined envelope version;
- keep legacy device-only QR supported during migration;
- activate device through QR runtime URL;
- import or confirm TOTP enrollment through existing provisioning/TOTP storage boundary;
- present partial failure states clearly:
  - device activated but TOTP failed;
  - TOTP imported but device activation failed should normally be avoided by ordering;
  - expired/consumed/revoked payload generic failure.

Expected files:

- `mobile/feature/device-onboarding/*`
- `mobile/app/src/main/.../deviceonboarding/*`
- `mobile/app/src/main/.../AuthenticatorApp.kt`
- `mobile/security/storage/*` only if TOTP persistence contract changes.

Verification:

- `:feature:device-onboarding:testDebugUnitTest`;
- `:app:testDebugUnitTest`;
- `:app:assembleDebug`;
- Android MCP install/start smoke;
- live scan of combined QR on test device.

Security review:

- QR payload and TOTP secret are not logged;
- credential-bearing URLs remain rejected;
- TOTP secret is stored only through encrypted TOTP store;
- device tokens remain separate from TOTP storage.

Iteration 4 result on `2026-04-29`:

- Android `:feature:device-onboarding` now accepts v2 combined QR envelopes `{ v, runtimeBaseUrl, activationPayload, totpProvisioningPayload }` while preserving v1 device-only envelopes and legacy raw `dac_...` compatibility.
- `mobile/app` activates the device first through the QR runtime URL, then imports the optional `otpauth://` TOTP provisioning payload through the existing encrypted TOTP store.
- The activation/import order avoids the bad partial state where TOTP is stored after failed device activation. If device activation succeeds but TOTP import fails, the raw QR material is cleared and the UI shows an explicit partial-success state.
- `AuthenticatorApp` activation logic was split into `DeviceOnboardingActivation.kt` to keep the composition root below the local file-size limit and preserve single-responsibility boundaries.
- Security review: QR payloads and TOTP secrets are not logged; credential-bearing runtime URLs remain rejected; TOTP storage stays inside `SecureTotpSecretStore`; device bearer/session storage remains separate from TOTP storage.

Verification:

- `:feature:device-onboarding:testDebugUnitTest :app:testDebugUnitTest` passed outside sandbox after sandbox `%USERPROFILE%\.gradle` wrapper lock `Access denied`.
- `:app:assembleDebug` passed outside sandbox.
- Fresh `app-debug.apk` installed and launched on `emulator-5554` through Android MCP; screenshot confirmed the app foreground with pending push, device onboarding and provisioning sections.
- Docs content sync passed `docs npm test` and `docs npm run build`.

Next continuation point:

- Iteration 5: Final Integrated Live Gate.

### Iteration 5. Final Integrated Live Gate

Goal: prove the complete server-owned contour.

Prerequisites:

- `ReferenceBackend` healthy at `https://admin.ghostring.ru:18444/`;
- Admin/API healthy at `https://admin.ghostring.ru:18443/`;
- no third-party tunnels;
- target client scope remains `challenges:read challenges:write`;
- physical Android has a fresh APK with combined QR and polling changes.

Live checks:

1. Create combined onboarding QR in Admin UI.
2. Scan once on Android.
3. Confirm artifact consumption and active push-capable device.
4. Confirm TOTP code appears on Android.
5. Start Desktop/WPF operation for the same `externalUserId`.
6. Confirm pending push appears without app restart.
7. Approve terminal flow.
8. Start another operation and deny terminal flow.
9. Start another operation and complete explicit TOTP fallback.
10. Record timestamps and residual latency.

Verification output:

- health/readiness snapshots;
- device/artifact sanitized state;
- session ids and terminal statuses;
- no secrets, access/refresh tokens, callback signing secrets, raw QR payloads or real push provider tokens in logs/docs.

Iteration 5 live preflight on `2026-04-29`:

- Public `ghostring` health remains green through Node/OpenSSL: `https://admin.ghostring.ru:18443/health/api` returned `200`, `https://admin.ghostring.ru:18444/health` returned `200`, and `https://admin.ghostring.ru:18444/api/reference/live-readiness` returned `isReadyForLiveRun=true` with no configuration issues.
- Playwright/Chromium in this workspace gets `ERR_CONNECTION_RESET` for both public TLS ports `18443` and `18444`; Node/OpenSSL succeeds, so browser MCP cannot currently be used as the live Admin UI driver for this contour.
- Android MCP sees only `emulator-5554`; no physical Android device is available through MCP in this session.
- Fresh `mobile/app/build/outputs/apk/debug/app-debug.apk` was installed and launched on `emulator-5554`; the app shows the pending push, device onboarding and provisioning sections.
- A live `POST https://admin.ghostring.ru:18444/api/reference/operations` for the canonical pilot `externalUserId=f1d6afaa-8a5d-4fd3-9f75-0a5c0177df81` returned `202 Accepted`, session `ef6c9c62448a4804957c1558e1c2122b`, status `Waiting`, `challengeCreatedAtUtc=2026-04-29T12:26:58.7912289Z`.
- After the foreground polling window, Android still showed an empty pending inbox and the reference session stayed `Waiting` with no callback or terminal timestamp.
- Active blocker: the current emulator/app needs fresh combined QR onboarding against the live runtime before the gate can continue to approve, deny and explicit online `TOTP` fallback checks. The current session environment has no admin password/env and no browser MCP path to create the combined QR through Admin UI.

Security review for this preflight:

- No admin password, access/refresh token, callback signing secret, raw QR payload, TOTP secret, activation payload or real push provider token was printed or stored in the vault.
- The live probe used only the public ReferenceBackend operation API and sanitized status fields.

### Iteration 6. Documentation Closure

Goal: make the final behavior operator-ready.

Scope:

- update `Reference Desktop Backend Stand`;
- update `mobile/README.md`;
- update docs app if user-facing setup flow changed;
- update `Current State`, `Implementation Map`, session note;
- add ADR only if combined onboarding package becomes a long-lived architectural decision beyond the MVP reference gate.

Verification:

- docs tests/build if docs app changes;
- no broad rerun if only vault markdown changes.

## Recommended Implementation Order

1. Iteration 1: ReferenceBackend explicit `TOTP` fallback.
2. Iteration 2: Android foreground pending refresh.
3. Iteration 3: combined onboarding backend/admin contract.
4. Iteration 4: Android combined QR consume flow.
5. Iteration 5: live gate.
6. Iteration 6: docs closure.

Reasoning:

- Iteration 1 closes the `409` root cause without waiting on QR changes.
- Iteration 2 removes the current restart requirement and improves push proof immediately.
- Iterations 3-4 are wider contract work and should happen after the current proof mechanics are stable.

## Context Reset

Use [[Final Integrated Verification Context Reset Prompt]] after clearing context.

## Related Notes

- [[Reference Desktop Backend Stand]]
- [[QR Device Onboarding Runtime URL Follow-Up]]
- [[QR Device Onboarding Follow-Up]]
- [[Push Delivery Latency Follow-Up]]
- [[Optional Boxed Integration Gateway]]
- [[../Decisions/ADR-035 - Official Dotnet Integration SDK and Reference Stand]]
- [[../Decisions/ADR-036 - Optional Boxed Integration Gateway and Access Connector Boundary]]
