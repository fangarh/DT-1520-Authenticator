# Admin Client Management Iteration Plan

## Status

Accepted working plan

## Goal

Разложить [[Admin Client Management Follow-Up]] на короткие implementation iterations, между которыми можно очищать контекст и продолжать работу по стабильному prompt/checkpoint.

Цель track-а: довести backend admin contour и `Admin UI` до operator-ready lifecycle для integration clients без ручных bootstrap workaround-ов.

## Why separate plan

Исходный follow-up фиксирует обязательный outcome, но не задает session-sized slices. Этот документ является рабочей декомпозицией для реализации.

## Global boundaries

### Must preserve

- `client_secret` показывается только один раз при create/rotate.
- `client_secret` не попадает в read model, logs, audit events, browser persistence or test snapshots.
- Все state-changing admin endpoints требуют admin cookie session + `CSRF`.
- Admin actions используют отдельные permissions, не смешанные с integration client scopes.
- Integration access tokens invalidated through existing `last_auth_state_changed_utc` semantics when secret/status changes.
- Existing CLI/migration lifecycle commands остаются compatible operational fallback, но не являются основным operator UX.

### Default permissions

Добавить отдельные admin permissions:

- `integration-clients.read`
- `integration-clients.write`

Если в коде будет выбран другой naming style, обновить этот документ, `Documentation Handoff Plan` и admin bootstrap docs.

### Scope whitelist

Admin UI не должен принимать произвольные scope strings. Использовать server-side whitelist текущих supported integration scopes:

- `challenges:read`
- `challenges:write`
- `enrollments:write`
- `devices:write`

Если фактический код содержит дополнительные supported scopes, sync this list from source before implementation.

## Iteration rules

- Одна итерация = одна Codex-сессия.
- После каждой итерации допустима очистка контекста.
- Каждая итерация закрывается только после:
  - automated tests relevant to changed layer
  - security review
  - docs/vault write-back
  - update latest session note
- UI iterations require Playwright verification.
- Если меняются admin permissions, обновить bootstrap/runbook/docs в той же итерации.

## Iteration 0 - Preflight and contract confirmation

Status: completed on `2026-04-27`.

### Objective

Подтвердить фактический backend/admin state и финализировать admin client management contract перед кодом.

### Work

- прочитать existing integration client lifecycle code:
  - `IntegrationClientLifecycleService`
  - `PostgresIntegrationClientLifecycleStore`
  - `PostgresIntegrationClientStore`
  - migration runner lifecycle commands
- прочитать existing admin auth/permission patterns
- проверить existing `Admin UI` feature layout and API client patterns
- подтвердить exact endpoint contract and DTO names

### Output

- если контракт меняется, обновить этот план перед implementation
- если контракт подтвержден, перейти к Iteration 1

### Confirmed preflight findings

- `IntegrationClientLifecycleService` уже закрывает operational `rotate secret` и `activate/deactivate`, но не умеет `create`, `update scopes` и operator-safe read model.
- `PostgresIntegrationClientStore` предназначен для runtime auth: читает только `is_active = true` и содержит `client_secret_hash`, поэтому его нельзя отдавать напрямую в admin read model.
- `PostgresIntegrationClientLifecycleStore` читает только lifecycle minimum (`clientId`, `isActive`, timestamps), поэтому для admin lifecycle нужен отдельный management/read store.
- Migration runner commands остаются fallback path: `rotate-integration-client-secret`, `deactivate-integration-client`, `activate-integration-client`.
- Admin auth pattern подтвержден: cookie session, `permission` claims, per-surface authorization policy constants, state-changing endpoints manually validate antiforgery and return stable `Problem`.
- `Admin UI` feature layout подтвержден: отдельный feature folder, локальный hook в `model/`, typed shared API client, contracts in `shared/types/admin-contracts.ts`, CSS modules per component.
- Supported integration scopes из кода подтверждены: `challenges:read`, `challenges:write`, `enrollments:write`, `devices:write`.

### Confirmed implementation contract

- Добавить admin permissions exactly as:
  - `integration-clients.read`
  - `integration-clients.write`
- Добавить auth policies:
  - `AdminIntegrationClientsRead`
  - `AdminIntegrationClientsWrite`
- Bootstrap admin whitelist обновить в той же backend итерации, где появятся permissions.
- Не переиспользовать `IIntegrationClientStore` как admin read model из-за `client_secret_hash` и active-only semantics.
- Создать отдельный admin-facing store/handlers, например:
  - `IAdminIntegrationClientStore`
  - `AdminListIntegrationClientsHandler`
  - `AdminCreateIntegrationClientHandler`
  - `AdminRotateIntegrationClientSecretHandler`
  - `AdminUpdateIntegrationClientScopesHandler`
  - `AdminSetIntegrationClientActiveStateHandler`
- Backend HTTP contracts use existing naming style:
  - `AdminIntegrationClientHttpResponse`
  - `AdminCreateIntegrationClientHttpRequest`
  - `AdminCreateIntegrationClientHttpResponse`
  - `AdminRotateIntegrationClientSecretHttpResponse`
  - `AdminUpdateIntegrationClientScopesHttpRequest`
- `AdminIntegrationClientHttpResponse` includes only sanitized metadata:
  - `clientId`
  - `tenantId`
  - `applicationClientId`
  - `status`
  - `allowedScopes`
  - `createdUtc`
  - `updatedUtc`
  - `lastSecretRotatedUtc`
  - `lastAuthStateChangedUtc`
- `client_secret_hash` and plaintext `client_secret` never appear in read/list responses, audit payloads, logs, browser persistence or test snapshots.
- Admin create/rotate endpoints generate secret server-side; operator-provided plaintext secret stays CLI-only fallback via existing migration runner.
- Admin lifecycle endpoints must carry `tenantId` in the path to make stale/cross-tenant UI actions fail closed.
- `clientId` is explicit operator input for the first admin implementation and must be validated as a route-safe stable identifier before storage.
- Current admin model has global permissions, not per-tenant ACL. Tenant safety for this track means explicit tenant filter/path validation, not operator-specific tenant grants.
- No new ADR is required for this contract; it refines the already accepted Admin Client Management follow-up.

### Verification

- no runtime tests required if no code changes
- security review of proposed contract

## Iteration 1 - Backend read model and admin transport

Status: completed on `2026-04-27`.

### Objective

Дать operator-safe read/list API для integration clients.

### Backend scope

- application contracts:
  - list integration clients by tenant
  - get integration client detail by client id or application binding if needed
- infrastructure store:
  - read `clientId`, `tenantId`, `applicationClientId`, allowed scopes, status, timestamps
  - no `client_secret_hash`
- admin endpoints:
  - `GET /api/v1/admin/tenants/{tenantId}/integration-clients`
  - no separate detail endpoint in the first slice unless implementation proves the list response is insufficient
- permission:
  - require `integration-clients.read`
- bootstrap permission whitelist update

### Tests

- handler/store tests for sanitized read model
- endpoint tests for `401/403/400/404/200`
- permission bootstrap tests if whitelist changes

### Security review checklist

- read model excludes secret hash
- tenant filtering is fail-closed
- operator without read permission gets `403`
- invalid filters return stable `Problem`

### Vault/docs write-back

- update this plan status
- update `Implementation Map`
- update `Current State`
- update session log

### Completed implementation notes

- Added separate sanitized admin read model/store:
  - `IAdminIntegrationClientStore`
  - `AdminListIntegrationClientsHandler`
  - `PostgresAdminIntegrationClientStore`
- Published `GET /api/v1/admin/tenants/{tenantId}/integration-clients`.
- Added admin policies/permissions:
  - `AdminIntegrationClientsRead`
  - `AdminIntegrationClientsWrite`
  - `integration-clients.read`
  - `integration-clients.write`
- Updated bootstrap admin permission whitelist for both new permissions.
- Read response intentionally includes only `clientId`, `tenantId`, `applicationClientId`, `status`, `allowedScopes`, timestamps and no `client_secret_hash` or plaintext `client_secret`.
- Targeted backend verification passed for `AdminIntegrationClient*` and `AdminUserBootstrapMaterialFactory` tests.

### Security review result

- Read model excludes secret hash and plaintext secret.
- Tenant filtering is explicit by route `tenantId` and returns `404` when no clients are visible for that tenant.
- Operator without `integration-clients.read` receives `403`.
- Empty tenant id returns stable `400 Problem`.

## Iteration 2 - Backend create client with one-time secret display

Status: completed on `2026-04-27`.

### Objective

Добавить operator-ready creation flow с one-time plaintext secret.

### Backend scope

- application command:
  - create integration client
  - generate high-entropy secret
  - hash and store secret
  - assign tenant/application binding
  - assign whitelisted scopes
  - return plaintext secret only in command response
  - reject duplicate `clientId` with stable `409`
  - reject operator-provided plaintext secret; admin path is server-generated only
- admin endpoint:
  - `POST /api/v1/admin/integration-clients`
- permission:
  - require `integration-clients.write`
- audit:
  - sanitized `admin_integration_client.created`

### Tests

- unit tests for secret generation/hash boundary
- handler tests for scope whitelist, duplicate client id, invalid tenant/application binding
- endpoint tests for `CSRF`, `401/403/400/409/201`
- audit tests proving no plaintext secret or hash in event payload

### Security review checklist

- plaintext secret returned only once
- no secret in logs/audit/read model
- unknown scopes fail closed
- state-changing endpoint requires `CSRF`

### Vault/docs write-back

- update admin setup docs if bootstrap permission set changes
- update implementation map and session log

### Completed implementation notes

- Added backend create command and transport:
  - `AdminCreateIntegrationClientHandler`
  - `POST /api/v1/admin/integration-clients`
  - `AdminCreateIntegrationClientHttpRequest`
  - `AdminCreateIntegrationClientHttpResponse`
- Extended `IAdminIntegrationClientStore` with create semantics and implemented atomic PostgreSQL insert for `auth.integration_clients + auth.integration_client_scopes`.
- Admin create generates the plaintext secret server-side, stores only PBKDF2 hash, and returns `clientSecret` only in the `201 Created` command response.
- Request validation rejects duplicate `clientId` with stable `409`, empty tenant/application identifiers, non route-safe client ids, unsupported scopes and operator-provided secret/hash fields.
- Added sanitized audit event `admin_integration_client.created`.
- Updated `backend/README.md` and `docs/` documentation data for the new create API.
- Targeted backend verification passed after rerun outside sandbox due `backend/artifacts/obj` write denial inside sandbox:
  - `dotnet build backend\OtpAuth.Infrastructure.Tests\OtpAuth.Infrastructure.Tests.csproj --no-restore -p:BuildInParallel=false -p:RestoreBuildInParallel=false -maxcpucount:1`
  - `dotnet test backend\OtpAuth.Infrastructure.Tests\OtpAuth.Infrastructure.Tests.csproj --no-build --filter "FullyQualifiedName~AdminIntegrationClient|FullyQualifiedName~AdminCreateIntegrationClient" --blame-hang-timeout 60s` (`19/19`)
- Full backend verification also passed through `backend/scripts/verify-backend.ps1`: `327/327` infrastructure tests and `19/19` worker tests; residual warning remains the known `IBM.Data.Db2` architecture mismatch in migrations.
- Docs verification passed after documentation updates: `docs npm test`, `docs npm run build`, `docs npm run test:e2e`.

### Security review result

- Plaintext `clientSecret` is returned only in create command response and is not stored in admin read model.
- `client_secret_hash` is written only to persistence and is not exposed in HTTP response or audit payload.
- State-changing endpoint requires admin cookie session, `integration-clients.write` and valid `CSRF`.
- Scope assignment is whitelist-only: `challenges:read`, `challenges:write`, `enrollments:write`, `devices:write`.
- `admin_integration_client.created` audit payload contains only sanitized metadata: admin identity, client id, tenant/application binding, status, scopes and timestamp.

## Iteration 3 - Backend lifecycle commands

Status: completed on `2026-04-27`.

### Objective

Закрыть backend lifecycle: rotate secret, update scopes, deactivate/reactivate.

### Backend scope

- commands/endpoints:
  - `POST /api/v1/admin/tenants/{tenantId}/integration-clients/{clientId}/rotate-secret`
  - `PUT /api/v1/admin/tenants/{tenantId}/integration-clients/{clientId}/scopes`
  - `POST /api/v1/admin/tenants/{tenantId}/integration-clients/{clientId}/deactivate`
  - `POST /api/v1/admin/tenants/{tenantId}/integration-clients/{clientId}/reactivate`
- preserve existing token invalidation behavior
- rotate secret returns plaintext secret only in command response and always server-generates it for admin path
- audit:
  - `admin_integration_client.secret_rotated`
  - `admin_integration_client.scopes_changed`
  - `admin_integration_client.deactivated`
  - `admin_integration_client.reactivated`

### Tests

- handler tests for token invalidation timestamp updates
- endpoint tests for `CSRF`, auth, conflict and not-found paths
- audit tests for sanitized payloads
- regression tests that read model never returns secret material

### Security review checklist

- rotated plaintext secret returned only once
- lifecycle commands are idempotent or return stable conflict semantics
- deactivated clients cannot obtain usable tokens
- scope update rejects unsupported scopes

### Vault/docs write-back

- update lifecycle docs and session log

### Completed implementation notes

- Added backend lifecycle command handlers and transport:
  - `AdminRotateIntegrationClientSecretHandler`
  - `AdminUpdateIntegrationClientScopesHandler`
  - `AdminSetIntegrationClientActiveStateHandler`
  - `POST /api/v1/admin/tenants/{tenantId}/integration-clients/{clientId}/rotate-secret`
  - `PUT /api/v1/admin/tenants/{tenantId}/integration-clients/{clientId}/scopes`
  - `POST /api/v1/admin/tenants/{tenantId}/integration-clients/{clientId}/deactivate`
  - `POST /api/v1/admin/tenants/{tenantId}/integration-clients/{clientId}/reactivate`
- Extended `IAdminIntegrationClientStore` and `PostgresAdminIntegrationClientStore` with tenant-bound `get`, rotate, scope update and active-state operations.
- Rotate secret generates plaintext only server-side and returns `clientSecret` only in the command response; stored value remains hash-only.
- Scope update is whitelist-only and replaces persisted scopes atomically with `last_auth_state_changed_utc` advancement.
- Deactivate/reactivate use stable `409` conflict semantics when the target status already matches and update `last_auth_state_changed_utc` on real state changes.
- Added sanitized audit events:
  - `admin_integration_client.secret_rotated`
  - `admin_integration_client.scopes_changed`
  - `admin_integration_client.deactivated`
  - `admin_integration_client.reactivated`
- Updated `backend/README.md` and `docs/` documentation data for lifecycle endpoints and permissions.
- Targeted verification passed after rerun outside sandbox due existing `backend/artifacts/obj` write denial:
  - `dotnet build backend\OtpAuth.Infrastructure.Tests\OtpAuth.Infrastructure.Tests.csproj --no-restore -p:BuildInParallel=false -p:RestoreBuildInParallel=false -maxcpucount:1`
  - `dotnet test backend\OtpAuth.Infrastructure.Tests\OtpAuth.Infrastructure.Tests.csproj --no-build --filter "FullyQualifiedName~AdminIntegrationClient" --blame-hang-timeout 60s` (`29/29`)
- Full backend verification passed through `backend/scripts/verify-backend.ps1`: `343/343` infrastructure tests and `19/19` worker tests; residual warning remains the known `IBM.Data.Db2` architecture mismatch in migrations.
- Docs verification passed after documentation updates: `docs npm test`, `docs npm run build`, `docs npm run test:e2e`.

### Security review result

- Rotated plaintext `clientSecret` is returned only in rotate command response and is not stored in read model or audit payload.
- Lifecycle endpoints require admin cookie session, `integration-clients.write` and valid `CSRF`.
- Tenant-bound routes fail closed: stale/cross-tenant `tenantId + clientId` actions return `404`.
- Unsupported scopes fail closed with stable `400 Problem`.
- Deactivate/reactivate conflicts are explicit `409`, so repeated destructive actions do not silently create misleading state transitions.
- Audit payloads remain sanitized and exclude `client_secret`, `client_secret_hash`, PBKDF2 material and transport/request details.

## Iteration 4 - Admin UI read/create workspace

Status: completed on `2026-04-27`.

### Objective

Добавить первый operator UI workspace для list/detail/create integration clients.

### UI scope

- feature folder for integration clients
- API client methods for list/detail/create
- list/table or compact inventory
- detail panel with sanitized metadata
- create form:
  - tenant id
  - application client id
  - client id or generated client id depending confirmed backend contract
  - scope selection from server-supported options
- one-time secret display panel
- explicit copy/discard UX without storing secret beyond current UI state

### Tests

- API client tests
- component tests for list/detail/create
- one-time secret display/discard tests
- `npm test`
- `npm run build`
- Playwright e2e for login -> open workspace -> create -> secret visible once

### Security review checklist

- secret not persisted to local/session storage
- secret hidden after discard/navigation/reload
- no secret in test snapshots
- UI never accepts arbitrary scope text if server supplies whitelist

### Vault/docs write-back

- update docs/admin guide placeholders if docs app exists
- update implementation map and session log

### Completed implementation notes

- Added `admin/src/features/integration-clients` workspace with lookup, inventory, sanitized detail and create panels.
- Extended `admin/src/shared/api/admin-api.ts` and `admin/src/shared/types/admin-contracts.ts` with list/create integration client contracts.
- Scope selection is checkbox-only from the supported whitelist:
  - `challenges:read`
  - `challenges:write`
  - `enrollments:write`
  - `devices:write`
- Create flow stores the generated `clientSecret` only in current React state, shows it in an explicit one-time panel and supports operator discard.
- Added checked-in Playwright regression `admin/e2e/admin-integration-clients.spec.ts` for `login -> load clients -> create -> secret visible -> discard -> reload`.
- Admin verification passed:
  - `npm test` (`36/36`)
  - `npm run build`
  - `npm run test:e2e` (`5/5`)

### Security review result

- Read/list UI renders only sanitized metadata from `AdminIntegrationClientHttpResponse`; no `client_secret_hash` or plaintext secret appears in list/detail state.
- Plaintext `clientSecret` is held only in transient React state after create, cleared on discard, load, reset and browser reload.
- The UI does not write generated secrets to `localStorage` or `sessionStorage`; Playwright verifies both remain empty after create/discard.
- Create uses the shared API client, so the state-changing request obtains a fresh `CSRF` token and sends it via `X-CSRF-TOKEN`.
- Scope input is whitelist-only through checkboxes and does not allow arbitrary scope strings.

## Iteration 5 - Admin UI lifecycle actions

Status: completed on `2026-04-27`.

### Objective

Expose rotate, scope update, deactivate and reactivate in `Admin UI`.

### UI scope

- rotate secret action with destructive confirmation
- one-time rotated secret display
- scope editor
- deactivate/reactivate actions
- disabled states for inactive clients
- clear operator messages for conflicts and permission errors

### Tests

- component tests for each action
- API client tests for request shape and `CSRF`
- Playwright e2e:
  - rotate secret -> one-time secret display
  - deactivate -> inactive status
  - reactivate -> active status
  - update scopes -> sanitized metadata update

### Security review checklist

- secret one-time display behavior matches backend
- state-changing calls use `CSRF`
- destructive actions require explicit confirmation
- no cross-tenant client action possible from stale UI selection

### Vault/docs write-back

- update admin documentation
- update implementation map/current state/session log

### Completed implementation notes

- Extended `admin/src/shared/api/admin-api.ts` with lifecycle command methods:
  - `rotateIntegrationClientSecret`
  - `updateIntegrationClientScopes`
  - `deactivateIntegrationClient`
  - `reactivateIntegrationClient`
- Added `IntegrationClientLifecyclePanel` under `admin/src/features/integration-clients` for selected-client lifecycle actions.
- Scope editing remains whitelist-only through the existing supported scope options:
  - `challenges:read`
  - `challenges:write`
  - `enrollments:write`
  - `devices:write`
- Rotate secret shows the returned plaintext only in a one-time rotated secret panel and clears it on discard, selection changes, reload and subsequent secret-bearing commands.
- Rotate/deactivate/reactivate actions require explicit operator confirmation; inactive clients disable rotate/deactivate and expose reactivate instead.
- Lifecycle calls are bound to the selected client's sanitized `tenantId + clientId`, not mutable lookup/create form drafts.
- Added checked-in Playwright regression coverage for `rotate secret -> one-time secret display -> discard`, `update scopes`, `deactivate` and `reactivate`.
- Admin verification passed:
  - `npm test` (`43/43`)
  - `npm run build`
  - `npm run test:e2e` (`6/6`)

### Security review result

- Rotated `clientSecret` is stored only in transient React state and is never written to `localStorage`, `sessionStorage`, read models or fixture snapshots.
- State-changing lifecycle calls reuse the shared admin API client, so each command obtains a fresh `CSRF` token and sends it via `X-CSRF-TOKEN`.
- Destructive/sensitive actions require explicit confirmation before the command button is enabled.
- Cross-tenant stale UI actions are fail-closed by using the selected client's own `tenantId + clientId` route binding rather than operator-editable form fields.
- Scope update remains whitelist-only through checkbox options; arbitrary scope strings cannot be submitted from the UI.

## Iteration 6 - End-to-end hardening and documentation closure

Status: completed on `2026-04-27`.

### Objective

Закрыть track как operator-ready feature.

### Work

- full backend verification
- full admin verification
- live/browser verification if runtime is available
- update docs:
  - admin installation/setup
  - permissions
  - client onboarding flow
  - secret rotation flow
  - deactivation/reactivation
  - troubleshooting
- update `Admin Client Management Follow-Up` status
- update `MVP Closure Iteration Plan`

### Tests

- `backend/scripts/verify-backend.ps1`
- `admin` `npm test`
- `admin` `npm run build`
- `admin` `npm run test:e2e`
- Playwright MCP/live check if UI changed and server is available

### Exit criteria

- operator can create integration client without CLI/seed workaround
- plaintext secret is shown only at create/rotate moment
- operator can list/view/update scopes/rotate/deactivate/reactivate
- audit trail is sanitized
- docs explain setup and operation
- vault marks [[Admin Client Management Follow-Up]] as completed

### Completed implementation notes

- Full backend verification passed through `backend/scripts/verify-backend.ps1` after rerun outside sandbox because sandbox denied writes to `backend/artifacts/bin/*/.msCoverageSourceRootsMapping_*`.
- Full backend result: `343/343` infrastructure tests and `19/19` worker tests passed; residual warning remains the known `IBM.Data.Db2` architecture mismatch in `OtpAuth.Migrations`.
- Full `Admin UI` verification passed:
  - `npm test` (`43/43`)
  - `npm run build`
  - `npm run test:e2e` (`6/6`)
- `docs/` and `admin/README.md` now document the operator-facing integration client onboarding, secret rotation, scope update, deactivate/reactivate and troubleshooting path.
- [[Admin Client Management Follow-Up]] is marked completed.
- [[MVP Closure Iteration Plan]] continuation point moves to [[QR Device Onboarding Follow-Up]].

### Security review result

- Plaintext `clientSecret` remains a one-time create/rotate response artifact and is not present in read models, audit payloads, browser persistence or checked-in fixture snapshots.
- Lifecycle commands remain protected by admin cookie session, `integration-clients.write` and `CSRF`.
- Scope updates remain whitelist-only.
- Tenant-bound lifecycle routes continue to fail closed through selected `tenantId + clientId`.
- Documentation updates do not add real secrets, raw tokens, signing material, private credential URLs, raw callback payloads or `pushToken` values.

## Context reset prompt

Use [[Admin Client Management Context Reset Prompt]] after clearing context.

## Related notes

- [[Admin Client Management Follow-Up]]
- [[MVP Closure Iteration Plan]]
- [[Documentation Handoff Plan]]
- [[../Decisions/ADR-035 - Official Dotnet Integration SDK and Reference Stand]]
