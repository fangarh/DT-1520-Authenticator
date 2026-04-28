# MVP Closure Iteration Plan

## Status

Accepted working plan

## Goal

Зафиксировать канонический путь от текущего состояния проекта к полноценному `MVP` без распыления на параллельные мелкие треки.

Этот план заменяет ad-hoc выбор следующего шага. Пока не закрыт `MVP`, новые инициативы нужно пропускать через этот документ.

## Why This Order

Текущий remaining gap уже не в базовых runtime-слайсах:

- `TOTP`, `backup codes`, `Device Registry`, `push approve/deny`, `push delivery`, `callbacks`, top-level `webhooks/events`
- `Admin UI MVP`
- `Installer MVP`
- `Android TOTP-first` и `Android Push Runtime`

Остаточный риск сместился в три зоны:

1. operator/support visibility по delivery/runtime состояниям
2. support closure вокруг device lifecycle
3. pilot-grade hardening и end-to-end proof

Поэтому путь к `MVP` фиксируется так:

1. `Iteration 1` — delivery observability and operator visibility
2. `Iteration 2` — device lifecycle admin/support surface
3. `Iteration 3` — pilot integration and final hardening

Опциональный следующий трек после этого:

- `Iteration 4` — management API/UI для integration clients и signing lifecycle

`Iteration 4` не нужна для формального `MVP`, если `Iteration 1-3` закрыты.

Обязательный follow-up сразу после первого подтвержденного ручного pilot-теста:

- [[Admin Client Management Follow-Up]]
- [[QR Device Onboarding Follow-Up]]
- [[Official Dotnet Integration SDK]]
- [[Reference Desktop Backend Stand]]
- [[Push Delivery Latency Follow-Up]]

Эти задачи не блокируют сам факт ручного pilot proof, но являются обязательными productization steps перед нормальным pilot usage. Latency hardening больше не должен диагностироваться в первую очередь через `ProjectManager`; основной повторный контур переносится на reference `Desktop + Backend` stand.

## Working Rules

- Не брать новый transport, новый factor или новый public product surface, пока не закрыт текущий iteration exit.
- В одной рабочей сессии двигать только один `slice` из текущей итерации.
- Каждая итерация закрывается только после:
  - unit/integration/browser или live verification
  - security review
  - синхронизации vault
- Если контекст сессии потерян, continuation reading path:
  1. [[../00 - Start Here]]
  2. [[../01 - Current State]]
  3. [[../Agent/Implementation Map]]
  4. эта заметка
  5. профильная заметка текущей итерации

## Iteration 1

### Name

Delivery Observability and Operator Visibility

### Objective

Сделать доставку внешних уведомлений операционно наблюдаемой:

- `challenge callbacks`
- top-level `webhooks/events`
- приоритетно read-only visibility для operator/admin contour

### In Scope

#### Slice 1A. Delivery status read model

- unified admin-facing read model для recent delivery records
- фильтры минимум по:
  - `tenantId`
  - `applicationClientId`
  - `channel` (`challenge_callback`, `webhook_event`)
  - `status` (`queued`, `delivered`, `failed`)
- без raw secret material и без повторной загрузки mutable domain state

Status on `2026-04-20`:

- закрыт backend read-model slice: добавлены unified admin-facing contracts/handler/store для recent `challenge_callback` + `webhook_event` deliveries
- read model фильтрует по `tenantId`, optional `applicationClientId`, `channel`, `status` и возвращает только sanitized destination без `userinfo/query/fragment`
- локальная verification подтверждена через `279` infra tests, `19` worker tests и `verify-backend.ps1`

#### Slice 1B. Admin API

- read-only admin endpoints для списка recent deliveries
- стабильный `Problem` contract для invalid filter и access denial
- отдельные permissions, если текущих `webhooks.read` недостаточно

Status on `2026-04-20`:

- закрыт read-only admin API slice: добавлен `GET /api/v1/admin/tenants/{tenantId}/delivery-statuses`
- endpoint принимает optional filters `applicationClientId`, `channel`, `status`, `limit`, reuses existing `webhooks.read` permission и возвращает sanitized recent deliveries из unified read model
- invalid `channel/status` filters получают стабильный `400 Problem`, access denial остается `403`, а unknown `applicationClientId` для tenant-а получает `404`
- локальная verification подтверждена через `284` infra tests и `verify-backend.ps1`

#### Slice 1C. Admin UI

- отдельный operator workspace для recent delivery status
- filter panel + inventory list + basic detail panel
- без retry/replay action в первой итерации

Status on `2026-04-20`:

- закрыт admin UI slice: `Admin UI` получил отдельный workspace для recent delivery outcomes поверх `GET /api/v1/admin/tenants/{tenantId}/delivery-statuses`
- workspace дает tenant/application-scoped filter panel, inventory list и read-only detail panel для `challenge_callback` + `webhook_event` без replay/retry actions
- UI отличает `queued`, `delivered`, `failed`, показывает sanitized destination/timing metadata и reuse existing `webhooks.read` permission без расширения auth surface
- локальная verification подтверждена через `admin` `npm test` (`25` tests), `npm run build` и `npm run test:e2e`

#### Slice 1D. Metrics baseline

- counters / summaries для `queued`, `delivered`, `failed`, `retrying`
- минимальный alert-friendly baseline в worker/api logs or metrics surface

Status on `2026-04-20`:

- закрыт metrics baseline slice: `challenge_callback` и `webhook_event` delivery stores теперь отдают sanitized status summary по `queued`, `delivered`, `failed` и `retrying`
- worker jobs `challenge_callback_delivery` и `webhook_event_delivery` публикуют этот baseline в job metrics/heartbeat и пишут alert-friendly summary line в worker logs без `URL`/payload/error-body leakage
- `retrying` считается как queued delivery с уже сделанной хотя бы одной попыткой, поэтому support получает минимально полезный backlog signal без нового public API surface
- локальная verification подтверждена через `verify-backend.ps1` (`284/284` infra tests, `19/19` worker tests)

### Out of Scope

- ручной replay delivery
- editing webhook payloads
- новый outbound transport
- distributed tracing platform

### Exit Criteria

- оператор видит последние `callback/webhook` delivery outcomes по tenant/application client
- UI отличает `queued`, `delivered`, `failed`
- тесты закрывают filter/auth/problem paths
- security review подтверждает отсутствие secret leakage

### Continuation Checkpoint

Если работа остановилась в середине `Iteration 1`, дальше читать:

1. [[../Integrations/Auth and Token Flows]]
2. [[../Decisions/ADR-033 - Top-Level Webhooks Use Subscription Outbox]]
3. код вокруг `WebhookEventDeliveryStore`, `ChallengeCallbackDeliveryStore`, `Admin UI`

## Iteration 2

### Name

Device Lifecycle Admin and Support Surface

### Objective

Довести support contour по устройствам до operationally usable состояния без расширения mobile/runtime semantics.

### In Scope

#### Slice 2A. Device admin read model

- current/recent devices по `tenantId + externalUserId`
- список active/revoked/blocked устройств
- safe metadata only:
  - `deviceId`
  - `platform`
  - lifecycle status
  - timestamps
  - `isPushCapable`

Status on `2026-04-20`:

- закрыт backend read-model slice: добавлены `AdminUserDevice*` contracts, `AdminListUserDevicesHandler`, `PostgresAdminDeviceStore` и lookup index `ix_devices_tenant_external_user_status`
- read model отдает current/recent `active|revoked|blocked` devices по `tenantId + externalUserId`, сортирует `current -> recent` и возвращает только safe metadata без `pushToken/publicKey/installationId/deviceName`
- локальная verification подтверждена через `290` infra tests, `19` worker tests и `verify-backend.ps1`

#### Slice 2B. Device admin command transport

- минимум `revoke`
- optional `block` только если потребуется отдельно от current replay-driven block semantics
- все actions под operator auth contour

Status on `2026-04-20`:

- закрыт backend transport slice: добавлены `GET /api/v1/admin/tenants/{tenantId}/users/{externalUserId}/devices` и `POST /api/v1/admin/tenants/{tenantId}/users/{externalUserId}/devices/{deviceId}/revoke`
- revoke transport требует operator admin session + `CSRF`, использует fail-closed request binding по `tenantId + externalUserId + deviceId` и reuses existing device lifecycle revoke side effects без нового public runtime semantics
- admin responses остаются sanitized: transport отдает только `deviceId/platform/status/timestamps/isPushCapable`, не раскрывая `deviceName`, `pushToken`, `publicKey` или `installationId`
- локальная verification подтверждена через `302` infra tests, `19` worker tests и `verify-backend.ps1`

#### Slice 2C. Admin UI

- support workspace по устройствам пользователя
- list + action panel
- destructive confirmation для revoke/block

Status on `2026-04-20`:

- закрыт admin UI slice: `Admin UI` получил новый workspace `user-devices` поверх `GET /api/v1/admin/tenants/{tenantId}/users/{externalUserId}/devices` и `POST .../devices/{deviceId}/revoke`
- workspace дает lookup по `tenantId + externalUserId`, inventory list для `active|revoked|blocked` устройств, detail/action panel с safe metadata only и destructive confirmation перед revoke
- UI action остается fail-closed: revoke использует только последний успешно загруженный scope, не раскрывает `installationId/deviceName/pushToken/publicKey`, а non-active devices не открывают новый support surface сверх already-existing `409`
- локальная verification подтверждена через `admin` `npm test` (`31` tests), `npm run build` и `npm run test:e2e` (`4` browser scenarios)

#### Slice 2D. Audit and webhook consistency

- admin device actions пишут sanitized audit
- side effects не расходятся с уже существующими `device.*` webhook events

Status on `2026-04-20`:

- закрыт audit/webhook consistency slice: operator revoke path теперь пишет отдельный append-only `admin_device.revoked` поверх existing `device.revoked` lifecycle audit
- admin audit строится из того же sanitized device snapshot, что и response path, и не раскрывает `installationId`, `deviceName`, `pushToken` или `publicKey`
- existing `device.revoked` webhook publication не меняет semantics и по-прежнему atomically queue-ится внутри device registry store
- локальная verification подтверждена через targeted admin/device tests (`13/13`) и общий `verify-backend.ps1`

### Out of Scope

- self-service device management
- push inbox redesign
- mobile UX refactor
- attestation hardening beyond already accepted `ADR`

### Exit Criteria

- support operator может найти устройства пользователя и безопасно revoke-ить нужное устройство
- backend/API/UI/tests закрывают auth/conflict/not-found paths
- webhook and audit side effects остаются согласованными

### Continuation Checkpoint

Если работа остановилась в середине `Iteration 2`, дальше читать:

1. [[../Architecture/Device Lifecycle Design]]
2. [[../Integrations/Auth and Token Flows]]
3. код вокруг `Device Registry`, `device.*` webhook publication и `Admin UI`

## Iteration 3

### Name

Pilot Integration and Final Hardening

### Objective

Проверить, что продукт готов к реальному pilot сценарию, а не только к локальным зелёным тестам.

### In Scope

#### Slice 3A. Pilot scenario definition

- выбрать один канонический integration story
- описать actors, steps, expected outcomes и failure handling
- привязать к конкретным repo entry points и runbook steps

Status on `2026-04-21`:

- `Slice 3A` зафиксирован через [[ProjectManager Pilot Integration Story]]
- canonical pilot app выбран как `ProjectManager`, потому что он уже использует existing `Keycloak + OIDC` primary auth contour и stable external subject через `Keycloak sub`
- canonical pilot operation выбрана как create/update `VCS instance` credentials
- `Authenticator` в этом pilot не заменяет `Keycloak`, а встраивается как backend-driven step-up `MFA` contour поверх already-authenticated operator session
- canonical `externalUserId` для integration path равен existing `Keycloak sub`, который `ProjectManager` уже использует в `JitProvisioningMiddleware`
- transport boundary зафиксирован как `ProjectManager backend -> Authenticator client_credentials -> signed callback back to ProjectManager`, без прямого SPA-to-Authenticator secret-bearing path

#### Slice 3B. Productization prerequisites and hardening gates

- rate limiting review
- retention/cleanup review
- secret rotation/runbook review
- backup/restore review
- installer handoff review

#### Slice 3B1. Admin client management

- закрыть [[Admin Client Management Follow-Up]]
- реализовывать по [[Admin Client Management Iteration Plan]]
- дать operator-ready lifecycle для integration clients без ручных bootstrap workaround-ов
- включить create, one-time secret display, scopes, rotate, deactivate/reactivate и sanitized audit

Status on `2026-04-27`:

- закрыт [[Admin Client Management Follow-Up]] как operator-ready productization track
- backend admin contour поддерживает list/create/rotate/update scopes/deactivate/reactivate integration clients с one-time `clientSecret` только в create/rotate response
- `Admin UI` содержит workspace для list/detail/create и lifecycle actions, включая discard one-time secret, whitelist-only scopes и explicit confirmation для sensitive actions
- full verification закрыта: `backend/scripts/verify-backend.ps1` зеленый (`343/343` infra, `19/19` worker), `admin` зеленый (`npm test` `43/43`, `npm run build`, `npm run test:e2e` `6/6`)
- docs/vault sync выполнен; следующий productization continuation point смещен на [[QR Device Onboarding Follow-Up]]

#### Slice 3B2. QR device onboarding

- закрыть [[QR Device Onboarding Follow-Up]]
- заменить debug/manual activation на operator-ready QR flow
- QR payload остается one-time opaque activation artifact with TTL and server-side consume/revoke semantics

Status on `2026-04-27`:

- закрыт [[QR Device Onboarding Follow-Up]] как operator-ready Android activation track
- backend/admin contour умеет create/list/revoke one-time onboarding artifacts поверх `auth.device_activation_codes`
- `Admin UI` содержит QR workspace: create показывает opaque activation payload только один раз в текущем UI state, list/detail не раскрывают payload/hash, revoke требует confirmation
- Android содержит production-oriented `:feature:device-onboarding`, `CameraX + ML Kit` scanner и `POST /api/v1/devices/activate-onboarding` без integration secret/token в mobile app
- full verification закрыта: backend `357/357` infra + `19/19` worker, admin `52/52` unit + `7/7` Playwright, Android `14/14` connected tests и live MCP smoke на `emulator-5554`
- следующий continuation point: [[Official Dotnet Integration SDK]]

#### Slice 3C. Official `.NET` SDK

- реализовать [[Official Dotnet Integration SDK]]
- first-class backend integration path через `Dt1520.Authenticator.Client` и `Dt1520.Authenticator.AspNetCore`
- desktop package остается UX/session helper layer и не хранит `client_secret`
- docs and samples входят в Definition of Done

Status on `2026-04-24`:

- первый ручной ProjectManager-created pilot подтвержден, но observed lag до Android prompt составляет около `~60s`
- follow-up оформлен отдельной заметкой [[Push Delivery Latency Follow-Up]]
- offline verification не проводилась; текущий checkpoint подтверждает только online/live contour
- после обсуждения latency hardening переносится на reference [[Reference Desktop Backend Stand]], потому что `ProjectManager` слишком шумный стенд для первичной диагностики delivery/polling path
- offline-code fallback в ближайшем плане означает: `Android` генерирует `TOTP` локально, а backend/`DT-1520` остаются online и сохраняют centralized verification/audit/rate limiting

#### Slice 3D. Reference Desktop + Backend stand and latency hardening

- реализовать [[Reference Desktop Backend Stand]]
- проверить SDK, Android QR activation, push approval и online `TOTP` fallback
- разобрать observed `~60s` lag уже на минимальном стенде
- измерить timestamps: desktop submit, backend receive, challenge creation, delivery enqueue, worker processing, pending inbox visibility, Android UI visible, terminal state
- принять pilot decision: tighter polling для открытого приложения или real push provider для near-real-time delivery
- зафиксировать acceptable pilot latency target

#### Slice 3E. End-to-end verification

- scripted or documented pilot verification path
- backend + admin + mobile/device where needed
- явная фиксация remaining environment blockers отдельно от code blockers

#### Slice 3F. MVP closure note

- зафиксировать, что именно считается `MVP done`
- перечислить оставшиеся post-MVP tracks отдельно

### Out of Scope

- enterprise auth expansion
- full tenant self-service product
- SAML/RADIUS/OIDC provider

### Exit Criteria

- есть один подтвержденный pilot-grade сценарий end-to-end
- hardening gaps либо закрыты, либо явно признаны post-MVP
- vault фиксирует формальное достижение `MVP-ready`

### Continuation Checkpoint

Если работа остановилась в середине `Iteration 3`, дальше читать:

1. эту заметку
2. [[Installer MVP Plan]]
3. [[../Integrations/Auth and Token Flows]]
4. [[ProjectManager Pilot Integration Story]]
5. runbooks и session note с последней verification попыткой

## Recommended Session Granularity

Чтобы не упираться в размер контекста, дробить работу так:

- одна сессия = один `slice`
- одна кодовая итерация = один transport/UI/read-model шаг
- после каждого `slice` обновлять:
  - эту заметку
  - [[../01 - Current State]]
  - последнюю session note

Не пытаться в одной сессии одновременно делать:

- новый backend read model
- новый admin UI workspace
- full hardening

Это разные continuation points и их нужно держать отдельно.

## Current Next Step

Следующий practical continuation point после закрытия `Iteration 3 / Slice 3B1`: [[QR Device Onboarding Follow-Up]].

Обязательный шаг после первого успешного ручного pilot-теста:

- [[Admin Client Management Follow-Up]] закрыт на `2026-04-27`
- закрыть [[QR Device Onboarding Follow-Up]]
- реализовать [[Official Dotnet Integration SDK]]
- пройти повторный цикл на [[Reference Desktop Backend Stand]]
- закрыть или явно принять как limitation [[Push Delivery Latency Follow-Up]]
