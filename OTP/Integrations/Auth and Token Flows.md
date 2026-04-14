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
