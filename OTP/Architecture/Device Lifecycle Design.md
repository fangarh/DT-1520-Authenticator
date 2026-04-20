# Device Lifecycle Design

## Status

Accepted working contract

Implementation status on `2026-04-17`: runtime slice `activate -> refresh -> revoke` implemented in backend with `auth.devices`, `auth.device_refresh_tokens`, `auth.device_activation_codes`, separate device JWT validator and unit/endpoint coverage. Device-bound `push approve/deny` contour тоже реализован поверх `DeviceBearer`: challenge хранит `target_device_id`, approve/deny валидируют binding + `Policy`, а create-path auto-bind-ит `push` только при единственном active push-capable device. Delivery slice поверх этого тоже реализован: `push` challenge atomically пишет row в `auth.push_challenge_deliveries`, worker lease-ит queued delivery через `PostgreSQL`, а trusted integration path теперь умеет deterministic routing через explicit `targetDeviceId` и `GET /api/v1/devices?externalUserId=...&pushCapableOnly=true`. Первый device-facing runtime read contour тоже закрыт: `GET /api/v1/devices/me/challenges/pending` возвращает sanitized pending `push` challenges, already bound к authenticated device bearer, без internal tracing identifiers вроде `correlationId`.

## Цель

Зафиксировать минимальный backend contract для `Device Registry`, чтобы следующие runtime slices не расходились между `Policy`, `Security`, `OpenAPI` и будущим `Android/push` contour.

## Scope этого design slice

В этот шаг входят:

- lifecycle статусы устройства
- activation / refresh / revoke contract
- storage policy для device refresh tokens
- server-side invalidation rules для device access tokens
- audit contract для device lifecycle

В этот шаг не входят:

- runtime implementation `Device Registry`
- `push approve/deny` implementation
- mandatory attestation
- operator UI для device support flows

## Device lifecycle states

- `pending`
- `active`
- `revoked`
- `blocked`

## Смысл состояний

### `pending`

- activation intent уже существует, но trust lifecycle еще не завершен
- устройство не может выполнять `push approve`
- refresh token pair еще не должна считаться runtime trust boundary

### `active`

- activation flow успешно завершен
- устройство может использовать device access token
- `push approve` допускается только при `active` state и дополнительной проверке `Policy`

### `revoked`

- устройство явно отключено интеграцией или оператором
- refresh и approve запрещены немедленно
- старые access token-ы перестают считаться валидными через `last_auth_state_changed_utc`

### `blocked`

- устройство заблокировано из-за security signal или явного server-side решения
- approve и refresh запрещены
- для возврата в рабочее состояние нужен новый controlled lifecycle, а не простое оживление старого refresh family

## API contour для `MVP`

### `POST /api/v1/devices/activate`

Назначение:

- завершить activation flow
- создать или финализировать device record
- выдать первую пару `device access token + refresh token`

Минимальные поля запроса:

- `tenantId`
- `externalUserId`
- `platform`
- `activationCode`
- `installationId`
- `deviceName`
- optional `pushToken`
- optional `publicKey`

Поведение:

- activation artifact должен быть one-time и иметь срок жизни
- duplicate activation не должна бесконтрольно переиспользовать уже выданный refresh family
- successful activation переводит устройство в `active`, пишет audit событие и обновляет `last_auth_state_changed_utc`

### `POST /api/v1/auth/device-tokens/refresh`

Назначение:

- атомарно погасить текущий refresh token
- выдать новый access token и новый refresh token

Поведение:

- refresh token never reusable
- reuse/expired/revoked token считается replay signal
- replay signal fail-closed revoke-ит текущий family и переводит устройство в `blocked`

### `POST /api/v1/devices/{deviceId}/revoke`

Назначение:

- немедленно отключить устройство
- остановить approve/refresh lifecycle

Поведение:

- обновляет `last_auth_state_changed_utc`
- revoke-ит активный refresh family
- пишет audit событие `device.revoked`

## Device token model

### Access token

- short-lived bearer token
- предназначен только для device-scoped runtime actions
- не переиспользует integration `client_credentials` credential type
- должен содержать как минимум:
  - `sub` или equivalent device subject
  - `device_id`
  - `tenant_id`
  - `scope`
  - `iat`
  - `exp`
  - `kid`

### Refresh token

- opaque random secret
- хранится только как hash
- живет в отдельной persistence модели
- rotate-ится на каждый successful refresh
- не логируется и не возвращается повторно вне activation/refresh response

## Persistence contract

### `auth.challenges`

Для device-bound `push` contour challenge persistence дополнительно хранит:

- `target_device_id`
- `approved_utc`
- `denied_utc`

Ожидаемые инварианты:

- `target_device_id` обязателен для `factor_type = push`
- `approve/deny` fail-closed отклоняются, если authenticated `device_id` не совпадает с bound target
- при нескольких active devices backend не выбирает target неявно; без deterministic binding `push` не должен становиться preferred factor
- integration client может снять ambiguity только явным `targetDeviceId`; без него multi-device path продолжает fail-closed fallback на `TOTP`

### `auth.push_challenge_deliveries`

Outbox-like delivery storage для фактической постановки `push` challenge на bound device:

- `id`
- `challenge_id`
- `tenant_id`
- `application_client_id`
- `external_user_id`
- `target_device_id`
- `status`
- `attempt_count`
- `next_attempt_utc`
- `last_attempt_utc`
- `delivered_utc`
- `last_error_code`
- `locked_until_utc`
- `provider_message_id`
- `created_utc`
- `updated_utc`

Ожидаемые инварианты:

- запись delivery создается атомарно вместе с `push` challenge
- для одного `challenge_id` существует не более одной delivery row
- worker берет due rows через lease/lock, а не через blind polling
- raw `pushToken` не дублируется в delivery table; при dispatch worker читает актуальный token из `auth.devices`
- permanent delivery failure не меняет challenge binding на другой device автоматически

### `auth.device_activation_codes`

Bootstrap/runtime activation artifact storage:

- `id`
- `tenant_id`
- `application_client_id`
- `external_user_id`
- `platform`
- `code_hash`
- `expires_utc`
- `consumed_utc`
- `created_utc`

Ожидаемые инварианты:

- activation code plaintext не хранится
- artifact consume-ится атомарно вместе с первой device/token issuance
- expired или уже consumed artifact снаружи маскируется как generic invalid/expired activation code

### `auth.devices`

Минимально нужны поля:

- `id`
- `tenant_id`
- `application_client_id`
- `external_user_id`
- `platform`
- `installation_id`
- `device_name`
- `status`
- `attestation_status`
- `push_token`
- `public_key`
- `activated_utc`
- `last_seen_utc`
- `last_auth_state_changed_utc`
- `revoked_utc`
- `blocked_utc`
- `created_utc`

Ожидаемые инварианты:

- для runtime token validation устройство должно читаться без join на произвольные UI-модели
- `last_auth_state_changed_utc` обновляется на каждом security-значимом lifecycle event
- `installation_id` помогает отличать новое app install состояние от старого record/token family

### `auth.device_refresh_tokens`

Минимально нужны поля:

- `id`
- `device_id`
- `token_family_id`
- `token_hash`
- `issued_utc`
- `expires_utc`
- `consumed_utc`
- `revoked_utc`
- `replaced_by_token_id`
- `created_utc`

Ожидаемые инварианты:

- в активном состоянии у token family только один current refresh token
- successful refresh атомарно помечает старый token как consumed и создает новый
- refresh token plaintext не хранится
- replay detection возможна по `consumed/revoked/expired` состоянию

## Policy boundary

- `Policy` не хранит device record, но обязана получать `deviceTrustState`
- `push` разрешается только при `deviceTrustState = active`
- `revoked` и `blocked` состояния всегда deny для security-critical device flows

## Audit contract

Append-only security trail должен фиксировать sanitized события:

- `device.activated`
- `device.token_refreshed`
- `device.refresh_reuse_detected`
- `device.revoked`
- `device.blocked`

Audit payload не должен содержать:

- access token
- refresh token
- `pushToken`
- `publicKey` целиком, если это не требуется для расследования
- activation code

## Следующий runtime slice после этого design step

Этот runtime slice реализован на `2026-04-17`, включая mobile-side pending inbox, biometric gate и device-bound `approve/deny` UX.

Следующий practical step поверх него:

1. довести provider-specific adapter и внешний delivery/`webhook` contour без изменения `Device Registry` и outbox contract
2. добавить operator/support flows вокруг device lifecycle и explicit device management beyond текущего runtime API
3. закрыть observability/hardening вокруг `push` delivery, refresh replay и pilot integration scenario
