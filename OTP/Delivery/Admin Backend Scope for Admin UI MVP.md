# Admin Backend Scope for Admin UI MVP

## Status

Accepted working guideline

## Implementation status

На `2026-04-15` `Slice A`, `Slice C` и `Slice D` реализованы в коде:

- добавлен отдельный `admin auth contour`
- backend выдает server-managed cookie session для browser operator flow
- реализованы `GET /api/v1/admin/auth/csrf-token`, `POST /api/v1/admin/auth/login`, `POST /api/v1/admin/auth/logout`, `GET /api/v1/admin/auth/session`
- добавлены `auth.admin_users` и `auth.admin_user_permissions` как bootstrap credential/permission store
- закрыты `CSRF`, login rate limiting, sanitized audit для login/logout и endpoint/unit tests
- реализован `GET /api/v1/admin/tenants/{tenantId}/users/{externalUserId}/enrollments/totp/current`
- admin read model возвращает current `TOTP` enrollment summary без `secretUri` и `qrCodePayload`
- добавлен явный `revoked_utc`, чтобы operator read model не выводил revoke timestamp косвенно
- реализованы `POST /api/v1/admin/enrollments/totp`, `POST /api/v1/admin/enrollments/totp/{enrollmentId}/confirm`, `POST /api/v1/admin/enrollments/totp/{enrollmentId}/replace`, `POST /api/v1/admin/enrollments/totp/{enrollmentId}/revoke`
- admin `start` использует явный `applicationClientId` или fail-closed auto-resolve только при единственном активном integration client-е tenant-а
- `confirm/replace/revoke` используют server-side admin lookup по `enrollmentId`, а не integration client context
- operator actions теперь пишут `admin_totp_enrollment.*` audit события с привязкой к `adminUserId`

Следующий незакрытый шаг по этой заметке: отдельный live backend/browser regression contour сверх уже оформленной checked-in scripted `Playwright` suite, если команде понадобится регулярная проверка против реального `dt-auth`.

## Цель

Разложить минимальный backend scope, необходимый для запуска `Admin UI MVP` после принятия `ADR-026` и `ADR-027`.

Этот scope не включает installer, device lifecycle, `push`, `backup codes` и полноценный admin product surface за пределами `TOTP` enrollment management.

## Основания

- [[../Decisions/ADR-026 - Admin UI Uses Separate Admin Auth Contour]]
- [[../Decisions/ADR-027 - Admin UI Uses Current Enrollment Read Model by User]]
- [[Admin UI MVP Plan]]

## Текущее ограничение

Сейчас backend уже имеет два auth contour:

- `JwtBearer`
- `IntegrationClientContext`
- policies на `scope`
- enrollment endpoints под `EnrollmentsWrite`
- отдельный cookie-based `admin auth contour` для browser operator session

Но для `Admin UI MVP` этого все еще недостаточно, потому что operator flow пока не имеет отдельного admin transport для enrollment command-ов.

## Минимальные backend slices

### Slice A. Admin auth foundation

#### Что нужно добавить

- отдельный `Admin API` auth contour
- cookie-based session model
- admin login endpoint
- admin logout endpoint
- минимальные admin authorization policies:
  - `AdminEnrollmentsRead`
  - `AdminEnrollmentsWrite`

#### Минимальная модель данных

- `admin_users`
- `admin_user_password_credentials` или другой bootstrap credential store
- `admin_user_roles` или эквивалентный permission mapping
- optional `admin_sessions`, если сессия не полностью self-contained

#### Минимальные требования

- пароль хранится только как strong password hash
- cookie: `HttpOnly`, `Secure`, `SameSite`
- state-changing admin endpoints требуют CSRF protection
- login/logout и неудачные попытки логина аудируются

#### Что пока не нужно

- полный `LDAP/AD`
- внешний `OIDC`
- self-service admin identity management
- сложная иерархия ролей

### Slice B. Admin identity and permission contracts

#### Что нужно добавить

- `AdminPrincipal` / `AdminContext`
- admin claims/permissions abstraction
- permission checks, отделенные от integration scopes

#### Минимальный набор прав

- `enrollments.read`
- `enrollments.write`

#### Почему отдельно

- нельзя переиспользовать `IntegrationClientContext`
- human operator и external integration client имеют разную модель доверия

### Slice C. Current enrollment read model by user

#### Что нужно добавить

Отдельный admin-facing endpoint, например:

- `GET /api/v1/admin/tenants/{tenantId}/users/{externalUserId}/enrollments/totp/current`

#### Что возвращает

- `enrollmentId`
- `status`
- `hasPendingReplacement`
- `confirmedAt`
- `revokedAt`
- metadata, нужную для operator decisions

#### Чего не возвращает

- `secretUri`
- `qrCodePayload`
- secret material

#### Persistence/read model

- current active-or-latest `TOTP` enrollment по `tenantId + externalUserId`
- отдельный query path для admin lookup
- без необходимости сразу строить полную историю enrollments

### Slice D. Admin enrollment command endpoints

#### Подход

Существующие enrollment use cases не нужно выбрасывать, но browser admin UI не должен ходить в integration endpoints.

Нужно добавить admin-facing endpoints:

- `POST /api/v1/admin/enrollments/totp`
- `POST /api/v1/admin/enrollments/totp/{enrollmentId}/confirm`
- `POST /api/v1/admin/enrollments/totp/{enrollmentId}/replace`
- `POST /api/v1/admin/enrollments/totp/{enrollmentId}/revoke`

#### Внутренняя реализация

- reuse существующих application handlers там, где это безопасно
- заменить `IntegrationClientContext` на admin-facing request context или отдельный application contract
- не смешивать admin permission checks и integration scope checks в одном transport contract

#### Практический вариант для первой итерации

- оставить доменную логику enrollment management
- вынести auth/authorization boundary в отдельный admin transport layer
- при необходимости добавить admin-specific application facade, чтобы не тащить integration semantics в UI-контур

### Slice E. Admin audit contour for operator actions

#### Нужно аудировать

- admin login success/failure
- enrollment start
- enrollment confirm
- enrollment replace
- enrollment revoke

#### Требования

- append-only
- без secret material
- с привязкой к `adminUserId`

### Slice F. Problem contract for admin UI

#### Нужно зафиксировать

- mapping ошибок в стабильные `Problem`-ответы для admin endpoints
- различение:
  - `401`
  - `403`
  - `404`
  - `409`
  - `422`, если появится policy denial на admin contour

#### Почему это важно

- frontend должен строить UX на стабильных категориях ошибок, а не на случайных строках `detail`

## Предлагаемая последовательность реализации

### Iteration 1. Admin auth bootstrap

- admin user store
- password hashing
- login/logout endpoints
- cookie auth
- admin policies
- unit tests

Статус:
реализовано, включая `CSRF`, login rate limit и endpoint-level tests.

### Iteration 2. Admin current enrollment read

- current enrollment query contract
- admin read endpoint по `tenantId + externalUserId`
- integration tests

Статус:
реализовано, включая dedicated admin endpoint, sanitized response contract и `401/403/404` endpoint tests.

### Iteration 3. Admin enrollment command transport

- admin start/confirm/replace/revoke endpoints
- transport mapping на existing enrollment use cases
- audit integration
- endpoint tests

Статус:
реализовано, включая fail-closed `applicationClientId` resolution, by-id admin lookup, `CSRF` enforcement на state-changing endpoints и `167/167` automated tests.

### Iteration 4. Hardening

- CSRF
- rate limiting для login
- secure cookie settings
- audit coverage
- Playwright-backed UI verification against admin contour

Статус:
реализовано в двух слоях: backend hardening закрыт endpoint/security tests, а browser regression оформлена checked-in scripted `Playwright` suite в `admin/e2e`; ранее пройденная live verification через `Playwright MCP` остается отдельным операционным подтверждением runtime contour.

## Минимальный набор тестов

### Unit tests

- password hashing and verification
- admin permission checks
- admin current enrollment query mapping
- problem mapping

### Integration / endpoint tests

- unauthenticated admin request -> `401`
- missing permission -> `403`
- unknown user/enrollment -> `404`
- conflict flows -> `409`
- admin read endpoint never returns provisioning artifacts
- admin command endpoints write audit events

### Security tests

- login brute-force guard или rate limit
- CSRF enforcement на state-changing admin endpoints
- cookies marked secure in non-dev profiles
- no secret leakage in admin responses

## Что можно переиспользовать из текущего кода

- enrollment domain/application handlers
- `PostgresTotpEnrollmentProvisioningStore`
- unified `SecurityAuditService`
- существующие enrollment state transitions
- существующие anti-bruteforce механизмы confirm/replace

## Что нельзя переиспользовать как есть

- `IntegrationClientContext`
- `EnrollmentsWrite` policy
- browser access через integration `client_credentials`

## Exit criteria

Backend считается готовым к реализации `Admin UI MVP`, когда:

- есть отдельный admin auth contour
- есть current enrollment read endpoint по `tenantId + externalUserId`
- есть admin-facing command endpoints для `start/confirm/replace/revoke`
- admin audit пишет operator actions
- endpoint/security tests закрывают auth, conflicts и secret non-leakage
