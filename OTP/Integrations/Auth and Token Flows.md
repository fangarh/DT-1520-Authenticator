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

Это все еще bootstrap-уровень, потому что revocation, introspection, secret rotation workflow и production token lifecycle пока не реализованы как полноценный auth subsystem.

## Текущий bootstrap auth subsystem

В коде backend теперь дополнительно есть:

- `POST /oauth2/introspect` для scope-limited introspection access token
- `POST /oauth2/revoke` для отзыва конкретного access token
- persistent store `auth.revoked_integration_access_tokens`
- runtime-проверка revoked token на входе через `JwtBearer` validation events
- JWT access token выпускается с `kid`, а validation идет по current + legacy signing keys

Security behavior:

- introspection и revoke требуют `client_id + client_secret`
- чужой токен не раскрывается: introspection возвращает `active=false`, revoke делает no-op
- revoked token перестает проходить на защищенные endpoint-ы
- bootstrap flow по-прежнему не заменяет полноценный auth subsystem: нет refresh token model, нет signing key rotation UI/API, нет token introspection caching, нет management API для client lifecycle

## Mobile device tokens

Для мобильного устройства в `v1` выбран отдельный bearer token flow:

1. Интеграционный backend или enrollment flow активирует устройство через `POST /api/v1/devices/activate`.
2. В ответе устройство получает первую пару токенов.
3. Для продления доступа используется `POST /api/v1/auth/device-tokens/refresh`.

## Почему не единый auth flow

- интеграционный клиент и мобильное устройство имеют разную модель доверия
- для внешних систем удобнее и понятнее `OAuth 2.0 client credentials`
- для устройства нужен lifecycle, завязанный на `Device Registry`

## Callback and webhook model

Есть два механизма уведомлений:

- top-level `webhooks` как канонические события платформы
- operation-level `callbacks` в `POST /api/v1/challenges`, если клиент передает callback URL

Это позволяет поддержать и продуктовый event model, и per-request callback сценарий.
