# QR Device Onboarding Runtime URL Follow-Up

## Status

In progress.

This follow-up was opened after the reference `Desktop + Backend` live attempt on `2026-04-28`.

The existing QR onboarding track is functionally closed, but the Android live flow still has a pilot shortcut: the app needs `-PdeviceRuntimeBaseUrl=...` at APK build time. That is not the desired product contract.

## Problem

Current QR payload contains only an opaque one-time activation payload:

```text
dac_<activationCodeId>.<secret>
```

The Android app sends this payload to `/api/v1/devices/activate-onboarding`, but it currently learns the runtime base URL only from:

```kotlin
BuildConfig.DEVICE_RUNTIME_BASE_URL
```

That means a freshly installed APK built without `-PdeviceRuntimeBaseUrl=https://admin.ghostring.ru:18443` can scan the QR but cannot activate the device. The user-facing symptom is:

```text
Runtime адрес не настроен. Проверьте сборку приложения.
```

## Decision Direction

Runtime address should come from the QR onboarding artifact, not from an APK-specific build flag.

Target QR envelope:

```json
{
  "v": 1,
  "runtimeBaseUrl": "https://admin.ghostring.ru:18443",
  "activationPayload": "dac_<activationCodeId>.<secret>"
}
```

The old `dac_...` payload format may remain temporarily supported for backward compatibility, but a production onboarding QR should carry `runtimeBaseUrl`.

## Security Constraints

- `runtimeBaseUrl` is public routing metadata, not a secret.
- `activationPayload` remains credential-like one-time material.
- QR must not carry trusted tenant/user/application claims; backend still derives those from the server-side activation artifact.
- Android must validate `runtimeBaseUrl` before use: `https`, non-empty host and no embedded credentials/userinfo.
- Activation payload remains one-time, TTL-bound and server-consumed atomically.
- Browser storage must not persist raw activation payload or QR envelope.
- Android secure storage may persist runtime URL only as device-session routing metadata.

## Similar Tail Scan

Scan on `2026-04-28` found the relevant APK-bound runtime tail:

- `mobile/app/build.gradle.kts` defines `deviceRuntimeBaseUrl` Gradle property.
- `mobile/app/build.gradle.kts` writes `BuildConfig.DEVICE_RUNTIME_BASE_URL`.
- `mobile/app/src/main/.../AuthenticatorApp.kt` uses `BuildConfig.DEVICE_RUNTIME_BASE_URL` to create `HttpDeviceRuntimeTransport`.

No equivalent mobile onboarding runtime URL source was found in QR payload parsing. Other URL occurrences in `admin`, `rdb_stand` and backend are normal runtime/dev configuration surfaces, not APK-bound mobile onboarding state.

## Iteration Plan

### Iteration 1. Admin QR Runtime Envelope

Goal:

- Admin UI QR should encode a runtime envelope instead of raw `activationPayload`.

Write scope:

- `admin/src/features/device-onboarding/DeviceOnboardingQrPanel.tsx`
- `admin/src/features/device-onboarding/DeviceOnboardingPanels.test.tsx`
- `admin/e2e/admin-device-onboarding.spec.ts`
- related admin config/types only if needed

Expected behavior:

- QR code value is JSON envelope with `v`, `runtimeBaseUrl` and `activationPayload`.
- Runtime base URL is derived from the current public Admin/API runtime in a controlled way.
- For current ghostring live runtime, URL is `https://admin.ghostring.ru:18443`.
- Activation payload remains visible/copyable only in current operator UI state.
- Browser storage non-persistence still holds.

Minimum stage verification:

- targeted admin unit/component tests for QR envelope generation and non-persistence
- lightweight build/type check only if shared config/types are changed

Iteration 1 result on `2026-04-28`:

- `admin/src/features/device-onboarding/model/deviceOnboardingQrEnvelope.ts` now builds the v1 QR envelope and derives `runtimeBaseUrl` from controlled admin runtime config: absolute `VITE_ADMIN_API_BASE_URL` when configured, otherwise the current public browser origin. Same-origin relative config resolves to the public origin.
- `admin/src/features/device-onboarding/DeviceOnboardingQrPanel.tsx` now gives `QRCodeSVG` a JSON value with `v`, `runtimeBaseUrl` and the existing backend-issued `activationPayload`; the raw payload is still displayed/copyable only from current React state.
- Backend contract is unchanged: `POST /api/v1/admin/device-onboarding-artifacts` still returns the one-time plaintext activation payload once, list/detail paths still do not return payload/hash, and the frontend only wraps that payload for QR scanning.
- Browser storage non-persistence remains part of component/e2e coverage; neither raw activation payload nor QR envelope is written to `localStorage` or `sessionStorage`.
- Security review: the QR contains public routing metadata plus one-time credential-like payload only; no tenant/user/application claims are added to the QR envelope or treated as trusted client-side data.

Targeted verification:

- `admin`: `npm test -- --run src/features/device-onboarding/model/deviceOnboardingQrEnvelope.test.ts src/features/device-onboarding/DeviceOnboardingPanels.test.tsx` -> `11/11` passed. Initial sandbox run failed with `spawn EPERM`; the identical command passed outside sandbox.
- `admin`: `npm run build` -> passed.
- `admin`: `npm run test:e2e -- admin-device-onboarding.spec.ts` -> `1/1` passed. Initial sandbox run failed on `test-results/.last-run.json` unlink; the identical targeted spec passed outside sandbox.

### Iteration 2. Android QR Envelope Parser

Goal:

- Android should parse both the new QR envelope and the legacy `dac_...` payload.

Write scope:

- `mobile/feature/device-onboarding/src/main/.../DeviceOnboardingPayload.kt`
- `mobile/feature/device-onboarding/src/test/.../DeviceOnboardingPayloadTest.kt`
- `mobile/feature/device-onboarding/src/test/.../DeviceOnboardingWorkflowTest.kt`

Expected behavior:

- New model exposes `activationPayload` and `runtimeBaseUrl`.
- JSON envelope is accepted.
- Raw `dac_...` remains accepted as legacy payload without runtime URL.
- Invalid runtime URL is rejected with sanitized error.
- Runtime URL validation is fail-closed.

Minimum stage verification:

- `:feature:device-onboarding:testDebugUnitTest`

Iteration 2 result on `2026-04-28`:

- `mobile/feature/device-onboarding/src/main/.../DeviceOnboardingPayload.kt` now parses both v1 JSON QR envelopes and legacy raw `dac_...` payloads.
- `DeviceOnboardingPayload` exposes `activationPayload` and nullable `runtimeBaseUrl`; the existing `value` alias remains mapped to `activationPayload` so app activation wiring is unchanged in this iteration.
- JSON envelope parsing requires `v: 1`, `activationPayload` and `runtimeBaseUrl`; malformed envelopes fail closed.
- Runtime URL validation is fail-closed: only `https` URLs with a non-empty host and no embedded credentials/userinfo are accepted. Validation errors are generic and do not echo credential-bearing URLs.
- Legacy raw `dac_...` payloads remain accepted with `runtimeBaseUrl = null`.
- Security review: QR still carries only public routing metadata plus one-time activation material; no tenant/user/application claims are trusted from the QR, and no runtime URL is persisted yet.

Targeted verification:

- Android MCP check: `emulator-5554` is available.
- `mobile`: `:feature:device-onboarding:testDebugUnitTest` passed. Initial sandbox run failed on `%USERPROFILE%\.gradle\...\gradle-9.3.1-bin.zip.lck` with `Access denied`; the identical command passed outside sandbox with `JAVA_HOME='C:\Program Files\Android\Android Studio\jbr'`.

### Iteration 3. Android Activation Uses QR Runtime URL

Goal:

- Device activation should use `runtimeBaseUrl` from QR when present.

Write scope:

- `mobile/app/src/main/.../AuthenticatorApp.kt`
- `mobile/app/src/main/.../deviceruntime/DeviceRuntimeSessionManager.kt`
- app/device onboarding tests

Expected behavior:

- New QR envelope activates without `BuildConfig.DEVICE_RUNTIME_BASE_URL`.
- Legacy `dac_...` QR with empty build config fails with a clear message: QR does not contain runtime address.
- Legacy `dac_...` QR may still use `BuildConfig.DEVICE_RUNTIME_BASE_URL` as temporary compatibility.
- `Runtime адрес не настроен` no longer appears for new QR envelope.

Minimum stage verification:

- `:feature:device-onboarding:testDebugUnitTest`
- `:app:testDebugUnitTest`
- `:app:assembleDebug` only if app wiring/build config changes

Iteration 3 result on `2026-04-28`:

- `mobile/app/src/main/.../AuthenticatorApp.kt` now resolves activation runtime from the scanned QR payload first: v1 envelopes create the onboarding `DeviceRuntimeSessionManager` with QR `runtimeBaseUrl`, while injected test managers still take precedence in tests.
- Legacy raw `dac_...` payloads remain temporarily supported through the existing `BuildConfig.DEVICE_RUNTIME_BASE_URL` fallback.
- Legacy raw payloads without a configured fallback now fail with a clear sanitized message that the QR does not contain a runtime address, instead of the old build-specific `Runtime адрес не настроен` message.
- `mobile/app/src/main/.../app/deviceonboarding/DeviceOnboardingRuntimeResolver.kt` keeps the selection logic testable and covered by `DeviceOnboardingRuntimeResolverTest`.
- Security review: `runtimeBaseUrl` remains public routing metadata, `activationPayload` is the only one-time credential-like value sent to activation, no tenant/user/application claims are trusted from QR, and this iteration does not persist runtime URL yet.

Targeted verification:

- Android MCP check: `emulator-5554` is available.
- `mobile`: `:feature:device-onboarding:testDebugUnitTest :app:testDebugUnitTest :app:assembleDebug` passed with `JAVA_HOME='C:\Program Files\Android\Android Studio\jbr'`.
- MCP live smoke: fresh `app-debug.apk` installed and launched on `emulator-5554`; screenshot confirmed the app reaches the `Device onboarding` UI without startup crash.
- `docs`: `npm test` and `npm run build` passed after documentation text sync.

### Iteration 4. Persist Runtime URL With Device Session

Goal:

- After successful QR activation, Android should keep the runtime URL needed for refresh, pending inbox, approve and deny.

Write scope:

- `mobile/security/storage`
- `mobile/app/src/main/.../deviceruntime`
- storage serializers/tests

Expected behavior:

- Runtime URL is saved in encrypted device session storage or a closely scoped encrypted runtime config record.
- Existing stored sessions fail safely or migrate explicitly.
- After app restart, pending push polling uses the persisted runtime URL.
- APK no longer requires `-PdeviceRuntimeBaseUrl` for productized QR onboarding.

Minimum stage verification:

- `:security:storage:testDebugUnitTest`
- `:app:testDebugUnitTest`
- storage migration/serialization regression tests for changed records

Iteration 4 result on `2026-04-28`:

- `mobile/security:storage` now persists optional `runtimeBaseUrl` inside the encrypted `StoredDeviceSession` snapshot.
- Session snapshot restore accepts legacy seven-part records without runtime URL and restores them with `runtimeBaseUrl = null`; new records serialize the runtime URL as an additional encrypted field.
- `DeviceRuntimeSessionManager` writes the manager runtime URL during activation and preserves it across token refresh, so refresh/pending/approve/deny can keep using the same runtime after app restart.
- `AuthenticatorApp` reads the encrypted stored session at startup and uses the persisted runtime URL when `BuildConfig.DEVICE_RUNTIME_BASE_URL` is empty; explicit build config still takes precedence.
- APK-bound runtime is no longer required for productized QR onboarding after a successful v1 QR activation, while legacy raw `dac_...` QR still requires the temporary build-config fallback.
- Security review: `runtimeBaseUrl` remains public routing metadata, is stored only in encrypted device session storage, and credential-bearing runtime URLs are rejected before session storage accepts them. Device tokens and one-time QR activation payloads remain separate from the URL field.

Targeted verification:

- Android MCP check: `emulator-5554` is available.
- `mobile`: `:security:storage:testDebugUnitTest :app:testDebugUnitTest` passed outside sandbox with `JAVA_HOME='C:\Program Files\Android\Android Studio\jbr'`; initial sandbox run failed on `%USERPROFILE%\.gradle\...\gradle-9.3.1-bin.zip.lck` with `Access denied`.
- `mobile`: `:feature:device-onboarding:testDebugUnitTest :app:assembleDebug` passed outside sandbox without `-PdeviceRuntimeBaseUrl`; initial sandbox run hit the same Gradle wrapper lock.
- MCP live smoke: fresh `app-debug.apk` installed on `emulator-5554`; direct `am start` launched `MainActivity`, and screenshot confirmed the app reaches the `Device onboarding` UI without startup crash.

### Iteration 5. QR Runtime Handoff And Documentation

Goal:

- Close the QR runtime URL implementation notes and prepare the integrated verification script/checklist without running the full live flow yet.

Documentation:

- update `OTP/01 - Current State.md`
- update `OTP/Agent/Implementation Map.md`
- update this note
- update `OTP/Product/Android Push Runtime Plan.md`
- update `admin`/`mobile`/`docs` handoff text as needed
- append session log

Minimum stage verification:

- targeted tests for any documentation-adjacent contract examples changed in this iteration
- no full live/e2e run here; keep the complete proof for the final integrated verification gate

Iteration 5 result on `2026-04-28`:

- QR runtime handoff is closed at documentation level: the productized contract is now `Admin UI QR envelope -> Android activation through QR runtimeBaseUrl -> encrypted device-session runtime URL reuse`.
- `OTP/01 - Current State.md`, `OTP/Agent/Implementation Map.md`, this note, `OTP/Product/Android Push Runtime Plan.md`, `mobile/README.md`, `docs/README.md` and docs app content describe that `-PdeviceRuntimeBaseUrl` is no longer required for productized QR onboarding after v1 QR activation.
- The complete live proof remains intentionally deferred to the `Final Integrated Verification Gate` after Iteration 10, because callback policy and tenant-centric admin routing are still planned changes that will affect the same end-to-end flow.
- Security review: no new runtime code or storage behavior was introduced in this iteration. The documented contract keeps `runtimeBaseUrl` as public routing metadata, keeps `activationPayload` one-time/current-session only, persists runtime URL only in encrypted Android device session storage, and does not treat QR content as trusted tenant/user/application claims.
- Residual risk: legacy raw `dac_...` QR payloads without runtime URL still require the temporary build-config fallback and are documented as compatibility/debug-only path.

Targeted verification:

- `docs`: `npm test` and `npm run build` pass for the documentation app after the handoff text updates.

### Iteration 6. Callback URL Policy Configuration

Goal:

- Move callback URL strictness from a hardcoded public-Internet assumption into an explicit deployment policy that can be configured through admin/runtime configuration.

Why after QR:

- First close the Android runtime URL source: APK build flags must stop being the onboarding contract.
- Then make callback validation contour-aware so closed/on-prem/private-network deployments do not need tunnels or artificial public DNS just to run a valid MVP/demo flow.

Policy model:

- `PublicInternet`: default and strict. Require `https`, reject `localhost`, `127.0.0.1`, private IP literals, embedded credentials/userinfo and invalid callback path.
- `PrivateNetwork`: allow private DNS/private IP ranges for closed contours. Prefer `https`; any relaxed transport rule must be explicit, visible to the operator and covered by security notes.
- `LocalDevelopment`: allow `localhost`, `127.0.0.1` and `http` only for local/dev/reference stand usage. Must not be the production default.

Path behavior:

- Keep the reference stand callback path `/api/reference/callbacks/dt1520` for `ReferenceBackend`.
- Do not make that path a global product invariant. Product/admin validation should allow an integration-specific callback path policy where appropriate.

Write scope:

- backend/admin callback URL policy contracts and validators
- admin UI configuration surface for selecting/seeing the policy
- `rdb_stand` options if the reference stand keeps its own local policy switch
- unit/e2e tests for all policies
- security review notes and vault/docs

Expected behavior:

- Existing production behavior remains strict by default.
- Closed contours can be configured intentionally instead of bypassing validation or rebuilding code.
- Operators can see which callback URL policy is active.
- Validation errors mention the active policy without echoing secrets or credential-bearing URLs.

Minimum stage verification:

- backend policy validator tests for `PublicInternet`, `PrivateNetwork` and `LocalDevelopment`
- targeted admin unit/component tests if UI configuration is added
- targeted `rdb_stand` tests if reference backend validation changes
- security review notes and vault/session update

Iteration 6 result on `2026-04-28`:

- Backend callback validation moved out of hardcoded public-Internet assumptions into `ChallengeCallbackUrlPolicy`.
- `PublicInternet` remains the default strict production mode: `HTTPS`, non-root callback path, no `localhost`, no loopback/private IP literals, no embedded credentials/userinfo and no URL fragments.
- `PrivateNetwork` explicitly allows private DNS/private IP HTTPS callbacks for closed/on-prem contours; `HTTP` is allowed only when `AllowInsecureHttp=true`.
- `LocalDevelopment` explicitly allows `localhost`/`127.0.0.1` and `HTTP` only for local/demo/reference stand usage.
- `CreateChallengeHandler` now validates `callbackUrl` through the configured policy and returns sanitized errors that mention the active policy without echoing the rejected URL.
- `OtpAuth.Api` binds `ChallengeCallbackUrlPolicy` from runtime configuration and exposes read-only `/api/v1/admin/runtime-configuration` for authenticated operators; the response contains only policy metadata, no callback URLs or secrets.
- `rdb_stand` now has matching `ReferenceBackend__CallbackUrlPolicyMode` and `ReferenceBackend__AllowInsecureCallbackHttp` options. `--preflight`/live-readiness expose the active policy so private/local relaxations are visible.
- Security review: default production behavior remains strict, relaxed modes are opt-in, credential-bearing URLs still fail closed, validation messages do not echo rejected URLs, and admin runtime configuration does not expose secret material.

Targeted verification:

- `backend`: `dotnet test backend\OtpAuth.Infrastructure.Tests\OtpAuth.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ChallengeCallbackUrlPolicyTests|FullyQualifiedName~CreateChallengeHandlerTests|FullyQualifiedName~AdminRuntimeConfigurationApiTests" -p:BuildInParallel=false -p:RestoreBuildInParallel=false -maxcpucount:1` -> `31/31` passed. First sandbox run hit `Access denied` in `backend/artifacts/obj`, so the same command was repeated outside sandbox.
- `rdb_stand`: `dotnet test rdb_stand\tests\ReferenceBackend.Tests\ReferenceBackend.Tests.csproj --filter "FullyQualifiedName~ReferenceBackendOptionsTests|FullyQualifiedName~LiveRunPreflightReporterTests" -maxcpucount:1` -> `13/13` passed. The interrupted run left stale `dotnet/MSBuild` processes and a stale `ReferenceBackend` process holding `ReferenceBackend.exe`; only those processes were stopped before rerun. Sandbox runs hit `Access denied` in `lib/artifacts/obj` and `rdb_stand/artifacts`, so the same command was repeated outside sandbox.

### Iteration 7. Tenant-Centric Admin Contracts

Goal:

- Prepare backend/admin contracts for a tenant-centric admin experience so operators no longer copy `tenantId`, `applicationClientId`, `clientId`, callback and runtime values between unrelated blocks.

Information model:

- Tenant / organization: business customer or deployment contour.
- Application: product/application inside a tenant.
- Integration client: API client with `client_id`, generated secret and scopes.
- User: end user under the tenant/application context.
- Device: user's authenticator device.

Write scope:

- backend/admin read models for tenant/application/integration-client directory
- create tenant command
- quick create flow: tenant + initial integration client by display name, with server-generated IDs
- advanced/manual create flow for migration/demo/recovery cases
- status model: `active`, `disabled`, `archived` and possibly `test`
- tests for validation, generated IDs, duplicate names, permissions and sanitized responses

Expected behavior:

- Basic operator flow needs only names; IDs are generated server-side.
- Manual flow remains available but clearly marked as advanced.
- Physical delete is avoided by default; deactivate/archive is the safe operator action.
- Read models never return `client_secret` or secret hashes.

Minimum stage verification:

- targeted backend unit/integration tests for list/create/status flows
- permission and CSRF tests for write endpoints
- security review for tenant boundary, secret non-disclosure and audit logging

Iteration 7 result on `2026-04-28`:

- Backend получил tenant-centric admin contract без UI-перестройки: `GET /api/v1/admin/tenants`, `GET /api/v1/admin/tenants/{tenantId}/directory`, `POST /api/v1/admin/tenants` и `POST /api/v1/admin/tenants/quick-create`.
- Добавлены permissions `tenants.read` и `tenants.write`; write endpoints требуют admin cookie session + `CSRF`.
- Добавлена схема `auth.tenants` и `auth.tenant_applications`; existing `auth.integration_clients` остается source of truth для client credentials, а tenant directory читает sanitized client metadata без `client_secret_hash`.
- Quick-create создает tenant, application и initial integration client по display name-ам, генерирует `tenantId`, `applicationClientId`, `clientId` и one-time `clientSecret` server-side.
- Manual create оставлен для advanced migration/demo/recovery случаев: operator может задать explicit `tenantId`, slug и status `active|disabled|archived|test`.
- Audit пишет sanitized события `admin_tenant.created` и `admin_tenant.quick_created`; plaintext secret/hash в audit/read models не попадает.
- Security review: tenant/application metadata не является secret material, quick-create rejects operator-provided secrets, read endpoints no-store/no-cache, write path защищен `tenants.write` + `CSRF`, physical delete не добавлен.

Targeted verification:

- `backend`: `dotnet test backend\OtpAuth.Infrastructure.Tests\OtpAuth.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AdminTenantDirectory|FullyQualifiedName~AdminUserBootstrapMaterialFactoryTests" -p:BuildInParallel=false -p:RestoreBuildInParallel=false -maxcpucount:1` -> `17/17` passed. Sandbox run hit `Access denied` in `backend/artifacts/obj`, identical command passed outside sandbox.
- `backend`: `dotnet build backend\OtpAuth.Migrations\OtpAuth.Migrations.csproj --no-restore -maxcpucount:1` -> passed outside sandbox after sandbox `Access denied`; residual warning remains the known `IBM.Data.Db2` architecture mismatch.
- `docs`: `npm test` (`3/3`) and `npm run build` passed after permission/tenant-directory handoff text sync.

### Iteration 8. Admin Tenant Directory Page

Goal:

- Replace the scattered MVP blocks with a primary `Tenants / Organizations` entry page.

UI scope:

- tenant table with name, `tenantId`, status, application/API-client counts, user count if available and created/updated timestamps
- quick create drawer/modal by name
- advanced/manual create drawer/modal
- status actions: disable/reactivate/archive
- navigation to tenant/client management page

Expected behavior:

- Operator can create a tenant and first API client without copy-paste.
- Generated IDs and one-time secrets are shown only at the correct one-time boundary.
- Existing admin blocks remain reachable only if still needed for transition, not as the primary workflow.
- Table is usable on desktop and narrow viewports without horizontal overflow.

Minimum stage verification:

- admin unit tests for table/create/status flows
- targeted browser/layout smoke only if the page layout changes materially
- one-time secret storage non-persistence covered by focused tests

Iteration 8 result on `2026-04-28`:

- `Admin UI` получил primary tenant directory workspace `admin/src/features/tenant-directory`.
- Workspace поддерживает `Load tenants`, selected tenant directory detail, quick create `tenant + application + initial API client` и advanced/manual tenant create.
- Shared admin API client теперь типизирует `listTenants`, `getTenantDirectory`, `createTenant` и `quickCreateTenant`.
- Quick create показывает generated `clientSecret` только в текущем React state, очищает его через discard/reload/follow-up commands и не пишет secret в `localStorage/sessionStorage`.
- Manual create поддерживает advanced status `active|test|disabled|archived`, но не возвращает secret material.
- E2E fixture получил tenant directory contracts; `admin/e2e/admin-tenants.spec.ts` покрывает load/detail, quick-create one-time secret boundary, manual create and narrow viewport overflow guard.
- Security review: tenant/application metadata не является secret material; one-time client secret не попадает в list/detail/read models, browser storage или fixture read paths; state-changing calls идут через existing admin cookie + `CSRF`; UI не показывает `client_secret_hash`.
- Residual scope note: backend status mutation endpoints еще не вводились, поэтому Iteration 8 показывает tenant status and supports status at manual create boundary, but disable/reactivate/archive commands remain for a later tenant management/status command slice.

Targeted verification:

- `admin`: `npm test -- --run src/features/tenant-directory/TenantDirectoryPanels.test.tsx src/shared/api/admin-api.test.ts` -> `19/19` passed. Initial sandbox run failed with `spawn EPERM`; identical command passed outside sandbox.
- `admin`: `npm run build` -> passed.
- `admin`: `npm run test:e2e -- admin-tenants.spec.ts` -> `3/3` passed. Initial sandbox run failed on `test-results/.last-run.json` unlink; identical targeted spec passed outside sandbox.

### Iteration 9. Tenant Management Page

Goal:

- Create the tenant/client management page that groups daily operations under one selected tenant context.

Initial tabs:

- Overview: identifiers, status, runtime/callback summary and warnings.
- API clients: list, create, rotate secret, scopes, deactivate/reactivate.
- Users: tenant users list and selected-user detail entry point.
- Devices / QR onboarding: device onboarding artifacts and device state under selected tenant/user where applicable.
- Callback / Runtime: runtime base URL, callback URL and active callback URL policy from Iteration 6.
- Reports: high-level challenge/device/onboarding metrics.

Selected user management:

- user profile and external subject
- devices bound to the user
- recent challenges
- reset/revoke onboarding/device state actions
- sanitized status and error history

Expected behavior:

- Tenant context is carried by route/state, not copied by hand.
- Actions use selected tenant/application/client IDs from the page context.
- Dangerous actions require explicit confirmation and write sanitized audit events.
- One-time secrets and QR activation payloads remain current-session only.

Minimum stage verification:

- admin unit tests for tab state, selected-user detail and tenant-context command routing
- backend tests if new read models/actions are required
- targeted browser/layout smoke only for changed tenant detail surfaces

Iteration 9 result on `2026-04-28`:

- `Admin UI` получил tenant management workspace `admin/src/features/tenant-management`, который открывается из выбранного tenant directory detail и группирует daily operations под одним selected tenant context.
- Workspace добавляет tabs `Overview`, `API clients`, `Users & devices`, `Runtime` и `Reports`; tenant/application/client IDs берутся из selected directory state, а не из ручного copy-paste lookup.
- `API clients` tab поддерживает tenant-bound create, rotate secret, update scopes, deactivate and reactivate через existing admin integration client endpoints; one-time create/rotate secrets живут только в текущем React state и очищаются при follow-up secret-bearing commands/discard.
- `Users & devices` tab поддерживает selected-user lookup, active device revoke и issue QR onboarding artifact для selected tenant/application/user; one-time activation payload показывается только current-session и не пишется в browser storage.
- `Runtime` tab читает `/api/v1/admin/runtime-configuration` и показывает callback URL policy metadata без callback URLs, signing secrets или raw payloads.
- `Reports` tab использует existing delivery status read model и selected-user device state для lightweight MVP summary без raw callback payloads, push tokens или QR activation payload persistence.
- E2E fixture и `admin/e2e/admin-tenants.spec.ts` расширены tenant-management flow: create/rotate client, load user devices, issue/discard QR payload, load runtime policy, refresh reports and narrow viewport overflow guard.
- Security review: state-changing calls reuse existing admin cookie + `CSRF`; commands are bound to selected `tenantId/applicationClientId/clientId/externalUserId`; `clientSecret`, secret hashes, activation payloads, push tokens and callback secrets are not introduced into read models, storage, reports or fixtures.
- Residual scope note: backend tenant status mutation endpoints and full tenant-wide metrics are still deferred to Iteration 10 / cleanup; reports are useful MVP summaries over existing read models, not a new analytics backend.

Targeted verification:

- `admin`: `npm test -- --run src/features/tenant-management/TenantManagementWorkspace.test.tsx` -> `3/3` passed. Initial sandbox run failed with `spawn EPERM`; identical command passed outside sandbox.
- `admin`: `npm test -- --run src/shared/api/admin-api.test.ts` -> `16/16` passed. Initial sandbox run failed with `spawn EPERM`; identical command passed outside sandbox.
- `admin`: `npm test` -> `67/67` passed.
- `admin`: `npm run build` -> passed.
- `admin`: `npm run test:e2e -- admin-tenants.spec.ts` -> `4/4` passed; first run exposed strict-locator regressions caused by new `* operations` headings, then passed after exact heading assertions.
- `docs`: `npm test` (`3/3`), `npm run build` and `npm run test:e2e` (`2/2`) passed after handoff text/test sync.

### Iteration 10. Admin Reports, Metrics And Cleanup

Goal:

- Add the useful MVP reporting layer and retire duplicated old blocks after the tenant-centric flow is proven.

Metrics scope:

- total users/devices under tenant
- active/inactive devices
- recent challenges by status
- callback delivery health
- QR onboarding artifacts created/consumed/expired/revoked
- last successful/failed approval activity

Cleanup scope:

- remove or demote legacy copy-paste blocks
- update docs screenshots/text if documentation includes admin flows
- update support/runbook instructions to use the tenant-centric path
- add migration notes for operators used to old MVP blocks

Expected behavior:

- Admin landing path is tenant-centric.
- Operator can complete the common setup and troubleshooting flow without copying IDs between sections.
- Reports do not expose secrets, raw callback payloads, push tokens or QR activation payloads.

Minimum stage verification:

- targeted admin unit tests for reports and removed legacy path guards
- targeted backend metrics/read-model tests if added
- docs/vault/session update

Iteration 10 result on `2026-04-28`:

- `Admin UI` reports tab now builds a selected-tenant report snapshot from existing safe read models: recent delivery statuses, selected-user device state and recent QR onboarding artifact metadata.
- Report summary now includes device active/inactive counts, delivery totals by delivered/failed/queued, callback vs webhook delivery counts, QR artifact counts by status and last approved/failed approval/device activity markers.
- `loadReports` fetches both `/delivery-statuses` and `/device-onboarding-artifacts` under the selected tenant/application/user context; QR read model responses still exclude activation payloads.
- Admin landing is tenant-centric when the session has `tenants.*`: `Tenant directory -> Tenant management` becomes the primary operator path, while the older copy-paste workspaces remain fallback-only for sessions without tenant permissions.
- Legacy Playwright specs now use a fixture session without tenant permissions, so fallback coverage remains explicit without making the legacy blocks the default operator path.
- Security review: no new backend endpoints or secret-bearing read paths were added; reports do not expose `clientSecret`, secret hashes, raw callback payloads, callback signing secrets, push tokens or QR activation payloads; state-changing commands still use existing CSRF-protected admin API client.
- Residual scope note: report numbers are MVP snapshots over existing read models, not a full tenant-wide analytics backend; a dedicated backend metrics/read-model slice can be added later if exact tenant-wide user/device totals are required without selecting a user.

Verification:

- `admin`: `npm test -- --run src/features/tenant-management/TenantManagementWorkspace.test.tsx src/shared/api/admin-api.test.ts` -> `19/19` passed. Initial sandbox run failed with `spawn EPERM`; identical command passed outside sandbox.
- `admin`: `npm test` -> `67/67` passed.
- `admin`: `npm run build` -> passed.
- `admin`: `npm run test:e2e -- admin-tenants.spec.ts` -> `4/4` passed.
- `admin`: `npm run test:e2e` -> `11/11` passed.
- `docs`: `npm test`, `npm run build` and `npm run test:e2e` -> passed.

Next continuation point:

- `Final Integrated Verification Gate`.

## Final Integrated Verification Gate

Run this only after Iteration 10 is implemented.

Purpose:

- Prove the full QR runtime, callback policy and tenant-centric admin workflow together.
- Catch regressions caused by interaction between backend contracts, admin routing, Android onboarding, reference stand and documentation.

Full QR live flow:

1. Build APK without `-PdeviceRuntimeBaseUrl`.
2. Install APK.
3. Create tenant and API client through the tenant-centric Admin UI.
4. Configure runtime/callback policy for the target contour.
5. Create onboarding QR in Admin UI.
6. Scan QR.
7. Android activates device using `runtimeBaseUrl` from QR.
8. Reference backend creates protected operation.
9. Android sees pending challenge.
10. Approve/deny works.

Full verification commands/checks:

- backend full verification, including admin contracts, callback policy and metrics/read models
- admin `npm test`, `npm run build`, `npm run test:e2e`
- admin Playwright responsive/visual checks for tenant directory, tenant detail and QR onboarding flows
- Android unit tests for onboarding/runtime/session storage and `:app:assembleDebug` without `-PdeviceRuntimeBaseUrl`
- Android instrumented/live verification through Android MCP on emulator/device
- `rdb_stand` restore/build/test and live-readiness/preflight
- docs app tests/build/e2e if docs changed
- final security review: no `client_secret`, secret hashes, callback signing secrets, raw callback payloads, push tokens, one-time QR activation payloads or credential-bearing URLs leak into UI storage, logs, reports or docs
- final vault/session update with exact commands, pass/fail results and residual risks

## Iteration 1 Starter Prompt

```text
Работай в D:\Projects\2026\DT-1520-Authenticator. Всегда отвечай по-русски. Следуй AGENTS.md: vault-first, tests always, security review, docs/vault sync.

Задача: QR Device Onboarding Runtime URL / Iteration 1.

Проблема: Android APK сейчас требует -PdeviceRuntimeBaseUrl, но runtimeBaseUrl должен приходить из QR. Начни с Admin UI QR contract.

Нужно:
1. Прочитать OTP/00, OTP/01, OTP/02, OTP/Agent/Implementation Map, OTP/Delivery/QR Device Onboarding Follow-Up и OTP/Delivery/QR Device Onboarding Runtime URL Follow-Up.
2. Изменить Admin UI так, чтобы QR code value был JSON envelope:
   { "v": 1, "runtimeBaseUrl": "<public DT-1520 runtime base URL>", "activationPayload": "<existing dac_...>" }
3. Runtime base URL брать из admin/runtime config или current public origin безопасным способом. Для live ghostring это https://admin.ghostring.ru:18443.
4. Не сохранять activationPayload/envelope в browser storage.
5. Обновить unit/e2e tests.
6. Провести security review: QR содержит public runtime URL + one-time activation payload; payload всё ещё one-time/current-session only.
7. Обновить docs/vault/session note.
8. На этом этапе запускать только минимально необходимые targeted admin tests/build checks для измененного QR contract. Полный admin/backend/mobile/rdb/docs regression оставить на Final Integrated Verification Gate после Iteration 10.

Не трогай Android в этой итерации, кроме если тест/тип контракта строго требует shared doc. В конце дай continuation prompt для Iteration 2 Android parser.
```

## Iteration 6 Starter Prompt

```text
Работай в D:\Projects\2026\DT-1520-Authenticator. Всегда отвечай по-русски. Следуй AGENTS.md: vault-first, tests always, security review, docs/vault sync. Для вопросов по библиотекам/SDK используй Context7.

Задача: QR Device Onboarding Runtime URL Follow-Up / Iteration 6 — Callback URL Policy Configuration.

Стартовый контекст:
1. Прочитать OTP/00, OTP/01, OTP/02, OTP/Agent/Implementation Map, OTP/Delivery/QR Device Onboarding Runtime URL Follow-Up, OTP/Delivery/Reference Desktop Backend Stand и релевантные security/admin notes.
2. Считать Iteration 1-5 закрытыми: QR уже несет runtimeBaseUrl, Android использует и сохраняет его, documentation handoff закрыт.
3. Не запускать полный live/e2e gate: он отложен до Final Integrated Verification Gate после Iteration 10.

Нужно:
1. Найти текущие backend/rdb_stand callback URL validators and admin/runtime configuration surfaces.
2. Спроектировать и реализовать explicit callback URL policy model: PublicInternet default, PrivateNetwork, LocalDevelopment.
3. Production default должен остаться strict: https, no localhost/private IP literals, no embedded credentials/userinfo, valid callback path.
4. Private/local relaxations должны быть явными, видимыми operator/runtime config и покрыты security notes.
5. Validation errors должны упоминать active policy без echo secret или credential-bearing URL.
6. Добавить backend/rdb_stand/admin tests по измененному scope; UI тесты только если появляется admin surface.
7. Обновить vault/docs/session note и выполнить security review.
```

## Open Discussion Before Execution

Перед стартом реализации нужно обсудить возможные дополнительные итерации:

- нужно ли backend/admin API явно возвращать `runtimeBaseUrl`, а не вычислять его во frontend;
- нужен ли signed QR envelope или достаточно one-time activation payload + HTTPS runtime URL;
- детали `Callback URL Policy Configuration`: где хранится policy, какие роли могут ее менять, нужен ли отдельный approval/warning для private/local modes;
- нужен ли multi-runtime/operator-selectable runtime profile для on-prem deployments сверх callback policy;
- детали tenant-centric admin rework: как назвать сущности в UI (`Организация`, `Приложение`, `API-клиент`, `Пользователь`), какие статусы нужны в MVP и где граница между tenant/application/client;
- нужен ли deep link/app link формат поверх JSON envelope;
- как мигрировать/обработать уже установленные APK и legacy QR artifacts.

## Related Notes

- [[QR Device Onboarding Follow-Up]]
- [[../Product/Android Push Runtime Plan]]
- [[Reference Desktop Backend Stand]]
- [[Push Delivery Latency Follow-Up]]
