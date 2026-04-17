# Device Lifecycle Design

## Status

Accepted working contract

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

1. `auth.devices` и `auth.device_refresh_tokens`
2. `ActivateDeviceHandler`
3. `RefreshDeviceTokenHandler`
4. `RevokeDeviceHandler`
5. runtime validator для device access token с `last_auth_state_changed_utc`
6. unit + endpoint tests для `active/revoked/blocked/replay`
