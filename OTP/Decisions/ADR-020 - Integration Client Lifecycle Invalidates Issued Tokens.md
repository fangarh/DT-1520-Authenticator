# ADR-020: Integration Client Lifecycle Invalidates Issued Tokens

## Status

Accepted

## Context

Для integration clients уже реализован bootstrap `OAuth 2.0 client_credentials` flow на short-lived JWT access tokens.

Если ограничиться только:

- хранением `client_secret_hash`
- ротацией секрета в БД
- флагом `is_active`

то возникают две security-проблемы:

1. уже выданные access token-ы продолжают жить до `exp`, даже после rotate/deactivate;
2. повторная активация клиента может случайно вернуть к жизни старые JWT.

Для `MVP` это недопустимо: lifecycle клиента должен реально менять auth state, а не только влиять на будущую выдачу токенов.

## Decision

- lifecycle integration clients реализуется как отдельный operational contour, а не как bootstrap HTTP management API
- для integration client вводится persisted timestamp `last_auth_state_changed_utc`
- любой security-значимый lifecycle event обновляет `last_auth_state_changed_utc`:
  - secret rotation
  - deactivate
  - reactivate
  - bootstrap reseed с изменением auth material
- JWT integration access token обязан содержать `iat`
- runtime validation и introspection считают token активным только если:
  - client все еще активен
  - token claims совпадают с активным client record
  - token не revoked
  - `iat >= last_auth_state_changed_utc`
- rotate/deactivate/reactivate выполняются через explicit operational commands migration runner-а и wrapper script, а не на старте `Api`

## Consequences

- secret rotation и deactivate начинают инвалидировать уже выданные access token-ы
- повторная активация клиента не оживляет старые JWT
- bootstrap contour остается безопаснее без преждевременного management API
- operational runbook должен явно описывать rotate/deactivate/reactivate команды
- полноценный admin/API lifecycle для integration clients может быть добавлен позже, но должен сохранить тот же security invariant через `last_auth_state_changed_utc`
