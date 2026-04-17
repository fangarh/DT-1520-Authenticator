# ADR-030: Device Registry Uses Rotating Refresh Tokens and Auth-State Invalidation

## Status

Accepted

## Context

После закрытия `backup codes` следующий backend gap сместился в `device lifecycle`.

В vault и `OpenAPI` уже были черновые endpoint-ы:

- `POST /api/v1/devices/activate`
- `POST /api/v1/auth/device-tokens/refresh`
- `POST /api/v1/devices/{deviceId}/revoke`

Но до этого решения не были зафиксированы самые критичные security-инварианты:

- как именно хранить refresh token устройства;
- как немедленно инвалидировать уже выданные device access token-ы после `revoke/block`;
- как обнаруживать refresh token replay;
- как не смешать integration `OAuth` contour и device auth contour в один credential type.

Для `MVP` это нельзя оставлять на потом, потому что именно здесь проходят `push approve` и будущий device-bound runtime access.

## Decision

- `Device Registry` остается каноническим источником истины для:
  - trust state устройства;
  - device auth state;
  - refresh token lifecycle;
  - server-side revoke/block semantics.
- устройство живет в lifecycle `pending -> active -> revoked|blocked`, где:
  - `pending` используется для не завершенного activation intent;
  - `active` требуется для `push approve` и device token refresh;
  - `revoked` и `blocked` запрещают approve и refresh;
  - повторная активация после `revoked/blocked` идет как новый lifecycle, а не как оживление старого token family.
- device access token является отдельным auth contour:
  - short-lived bearer token;
  - отдельная auth scheme/validator и отдельная claim semantics относительно integration `OAuth`;
  - допускается reuse общей signing infrastructure, но не общей credential model.
- для device record вводится persisted timestamp `last_auth_state_changed_utc`;
  следующие события обязаны его обновлять:
  - activation success;
  - refresh family revoke;
  - explicit device revoke;
  - device block;
  - future admin/manual trust-state changes.
- device access token обязан содержать `iat`;
  runtime validation считает token активным только если:
  - устройство все еще `active`;
  - token относится к текущему device record;
  - `iat >= last_auth_state_changed_utc`.
- refresh token устройства:
  - непрозрачный случайный secret, а не JWT;
  - хранится в persistence только как hash;
  - живет в `PostgreSQL` как rotating one-time token family;
  - каждый successful refresh атомарно consume-ит текущий token и выдает новый access + refresh pair.
- повторное использование уже consume-нутого, revoked или просроченного refresh token рассматривается как replay signal;
  `MVP` fail-closed behavior:
  - активный token family немедленно revoke-ится;
  - устройство переводится в `blocked`;
  - событие пишется в append-only security audit trail.
- audit contour должен фиксировать sanitized device lifecycle events:
  - `device.activated`
  - `device.token_refreshed`
  - `device.refresh_reuse_detected`
  - `device.revoked`
  - `device.blocked`

## Consequences

- storage policy для device refresh tokens и future device auth contour перестает быть open question
- `revoke/block` начинает иметь немедленную server-side силу, а не только UI-эффект
- будущий `push` contour может безопасно опираться на `Device Registry`, не проверяя локальную эвристику клиента
- runtime implementation должна строиться вокруг persistent refresh store, `last_auth_state_changed_utc` и отдельного device token validator
- будущий admin/API management surface для устройств обязан сохранить те же security invariants и не может упрощать refresh flow до long-lived static secret
