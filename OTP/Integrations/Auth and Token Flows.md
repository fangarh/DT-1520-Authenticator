# Auth and Token Flows

## Integration clients

Для внешних систем в `v1` выбран `OAuth 2.0 client credentials`.

Поток:

1. Интеграционный клиент получает токен через `POST /oauth2/token`.
2. Использует bearer token для вызовов `Challenges`, `Enrollments`, `Devices`.
3. Доступ ограничивается scope-ами.

Scope-ы текущего draft:

- `challenges:write`
- `challenges:read`
- `enrollments:write`
- `devices:write`

## Admin/operator auth

`Admin UI` не использует integration `OAuth 2.0 client_credentials`.

Для browser-based operator surface принимается отдельный `admin auth contour` с отдельной session model и role/permission boundary.

Причины:

- human operator и integration client имеют разную модель доверия
- browser UI не должен получать `client_secret`
- admin actions должны аудироваться и авторизоваться отдельно от machine-to-machine flows

## Текущий bootstrap implementation status

В коде backend уже есть рабочий bootstrap `client_credentials` flow:

- `POST /oauth2/token` выпускает short-lived bearer token для bootstrap integration client registry
- JWT содержит `client_id`, `tenant_id`, `application_client_id` и `scope`
- `CreateChallenge` проверяет совпадение request scope с аутентифицированным `tenant/application client`
- `GetChallenge` и `VerifyTotp` делают scoped lookup, чтобы чужой `challengeId` не раскрывал существование ресурса
- bootstrap client registry хранится в `PostgreSQL`
- `Challenge` state теперь тоже хранится в `PostgreSQL`, а не в process-local memory
- `VerifyTotp` использует активный `TOTP` enrollment из `PostgreSQL`, а не process-local derived secret
- bootstrap client seed выполняется явно через migration runner и env secret
- на рабочем сервере `dt-auth` bootstrap token issuance уже проверен end-to-end через `/oauth2/token`
- flow `CreateChallenge -> GetChallenge -> VerifyTotp` уже проверен end-to-end через тот же bootstrap auth слой и реальный `PostgreSQL`

Это все еще bootstrap-уровень, потому что production token lifecycle, signing key lifecycle и полноценный management API пока не реализованы как завершенный auth subsystem.

## Текущий bootstrap auth subsystem

В коде backend теперь дополнительно есть:

- `POST /oauth2/introspect` для scope-limited introspection access token
- `POST /oauth2/revoke` для отзыва конкретного access token
- persistent store `auth.revoked_integration_access_tokens`
- runtime-проверка revoked token на входе через `JwtBearer` validation events
- JWT access token выпускается с `kid`, а validation идет по current + legacy signing keys
- legacy signing key может получить `RetireAtUtc`; после этой точки runtime/introspection больше не принимают токены с retired `kid`
- JWT access token выпускается с `iat`, а runtime/introspection сверяют его с persisted `last_auth_state_changed_utc` integration client-а
- operational commands для integration client lifecycle:
  - `rotate-integration-client-secret <client-id>`
  - `deactivate-integration-client <client-id>`
  - `activate-integration-client <client-id>`
- operational inspection для signing key lifecycle:
  - `inspect-signing-key-lifecycle`
- operational audit/reporting для signing key lifecycle:
  - `audit-signing-key-lifecycle`
  - `list-signing-key-lifecycle-audit-events [limit]`

Security behavior:

- introspection и revoke требуют `client_id + client_secret`
- чужой токен не раскрывается: introspection возвращает `active=false`, revoke делает no-op
- revoked token перестает проходить на защищенные endpoint-ы
- rotate/deactivate/reactivate integration client инвалидируют уже выданные JWT через persisted `client auth state`
- signing key rollout выполняется через смену current key, а старый key переводится в legacy с `RetireAtUtc = rollout time + token lifetime + clock skew`
- audit snapshot signing key lifecycle пишется append-only в `auth.signing_key_audit_events` и содержит только sanitized metadata без signing material
- bootstrap flow по-прежнему не заменяет полноценный auth subsystem: нет refresh token model, нет signing key rotation UI/API, нет token introspection caching, нет admin/API management surface для client lifecycle и нет общего audit contour для остальных security-событий

## Mobile device tokens

Для мобильного устройства в `v1` выбран отдельный bearer token flow:

1. Интеграционный backend или enrollment flow активирует устройство через `POST /api/v1/devices/activate`.
2. В ответе устройство получает первую пару токенов.
3. Для продления доступа используется `POST /api/v1/auth/device-tokens/refresh`.

## Почему не единый auth flow

- интеграционный клиент и мобильное устройство имеют разную модель доверия
- для внешних систем удобнее и понятнее `OAuth 2.0 client credentials`
- для устройства нужен lifecycle, завязанный на `Device Registry`
- для `Admin UI` нужен отдельный human-operator auth contour, а не reuse integration auth

## Callback and webhook model

Есть два механизма уведомлений:

- top-level `webhooks` как канонические события платформы
- operation-level `callbacks` в `POST /api/v1/challenges`, если клиент передает callback URL

Это позволяет поддержать и продуктовый event model, и per-request callback сценарий.
