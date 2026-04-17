# OpenAPI v1 Outline

## Цель версии

Дать минимальный, но полноценный интеграционный контракт для `MVP`.

## Ресурсы

### Auth

- `POST /oauth2/token`
- `POST /api/v1/auth/device-tokens/refresh`

### Challenges

- `POST /api/v1/challenges`
- `GET /api/v1/challenges/{challengeId}`
- `POST /api/v1/challenges/{challengeId}/verify-totp`
- `POST /api/v1/challenges/{challengeId}/approve`
- `POST /api/v1/challenges/{challengeId}/deny`

### Enrollments

- `GET /api/v1/enrollments/totp/{enrollmentId}`
- `POST /api/v1/enrollments/totp`
- `POST /api/v1/enrollments/totp/{enrollmentId}/confirm`
- `POST /api/v1/enrollments/totp/{enrollmentId}/replace`
- `POST /api/v1/enrollments/totp/{enrollmentId}/revoke`

### Devices

- `POST /api/v1/devices/activate`
- `POST /api/v1/devices/{deviceId}/revoke`

## Что зафиксировано в текущем draft

- `OpenAPI 3.1`
- `OAuth 2.0 client credentials` для интеграционного клиента
- bearer token flow для мобильного устройства
- единая схема ошибок `Problem`
- top-level `webhooks`
- operation-level `callbacks` для `createChallenge`
- `Idempotency-Key` для создания `challenge`
- response headers для request tracing и rate limiting
- унифицированные enum-значения по статусам и факторам

## Основные статусы challenge

- `pending`
- `approved`
- `denied`
- `expired`
- `failed`

## Обязательные поля при создании challenge

- `tenantId`
- `applicationClientId`
- `subject.externalUserId`
- `operation.type`
- `preferredFactors`
- `correlationId`

## Webhooks

- `challenge.approved`
- `challenge.denied`
- `challenge.expired`
- `device.enrolled`
- `factor.revoked`

## Следующий шаг

Канонический machine-readable черновик лежит в [[openapi-v1.yaml]].

Для `TOTP-first` enrollment artifact visibility и семантика `secretUri`/`qrCodePayload` отдельно зафиксированы в [[TOTP Provisioning Contract]].
