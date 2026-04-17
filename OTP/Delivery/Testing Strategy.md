# Testing Strategy

## Status

Draft

## Цель

Определить обязательные тесты и quality gate для `backend`, `admin` и `mobile`.

## Общий принцип

Любая завершенная задача должна иметь automated tests на своем уровне. Security-критичные сценарии не допускают отложенного покрытия.

## Backend

### Unit tests

- доменные правила `Challenge`
- правила `TOTP` verification
- правила `backup code`
- policy selection
- token lifecycle logic

### Integration tests

- endpoint -> application -> persistence flow
- auth и scope enforcement
- идемпотентность state-changing endpoints
- корректность сериализации `Problem`-ошибок

### Contract tests

- соответствие реализованных endpoint-ов `openapi-v1.yaml`
- обратная совместимость для `v1`

### Security tests

- `401/403` на защищенных endpoint-ах
- валидация некорректного ввода
- rate limiting
- anti-replay
- token revocation and introspection
- invalidation issued tokens after integration client lifecycle change
- отсутствие секретов в response payload

## Admin

### Unit tests

- state and permission logic
- form validation
- маппинг API errors в UI state

### Component tests

- ключевые формы и таблицы
- destructive actions с confirm flow

### E2E tests

- login и базовый operator flow
- enrollment flow
- device revoke flow

### Security checks

- UI не рендерит секреты повторно
- ошибки не раскрывают служебные детали
- опасные действия скрыты или заблокированы без нужной роли

## Mobile

### Unit tests

- локальная логика `TOTP`
- форматирование challenge state
- token/session lifecycle helpers

### Instrumented/UI tests

- enrollment flow
- approve/deny flow
- biometric gate integration

### Security checks

- секреты не попадают в обычные логи
- локальное хранилище использует защищенный storage
- revoke и logout реально очищают локальный доступ

## Definition of Done

Задача считается завершенной только если:

- код реализован
- тесты добавлены и проходят
- security review для изменения выполнен
- обновлен `OTP/` при изменении решений или реализации

## Первый обязательный тестовый пакет для backend slice

- `CreateChallenge` happy path
- `CreateChallenge` unauthorized client
- `CreateChallenge` tenant/application scoping enforced
- `IssueIntegrationToken` valid `client_credentials` request
- `IssueIntegrationToken` invalid client secret
- `IssueIntegrationToken` forbidden scope rejected
- `IntrospectIntegrationToken` returns `active=false` for foreign or revoked token
- `RevokeIntegrationToken` revokes only own token and does not disclose foreign token ownership
- `GetChallengeStatus` happy path
- `GetChallengeStatus` hides foreign challenge outside client scope
- `VerifyTotp` success
- `VerifyTotp` invalid code
- `VerifyTotp` expired challenge
- `VerifyTotp` unauthorized or out-of-scope client
- `VerifyTotp` rate limit exceeded
- `VerifyTotp` replay attempt rejected

## Текущее backend-покрытие bootstrap slice

- `VerifyTotp` покрыт unit tests для `success`, `invalid code`, `expired`, `invalid state`, `rate limited`, `replay detected`, `access denied`, `out-of-scope`
- `PostgresTotpVerifier` покрыт unit tests для `valid`, `invalid`, `missing enrollment`, `replay detected`
- runtime limit threshold покрыт unit tests на decision layer
- bootstrap OAuth покрыт unit tests для `token issuance`, `introspection`, `revocation` и runtime rejected revoked token
- integration client lifecycle покрыт unit tests для `rotate secret`, `deactivate/reactivate`, `iat` claim emission и invalidation токена после `last_auth_state_changed_utc`
- key rotation покрыта unit tests для `legacy TOTP key decrypt`, JWT `kid` emission, validation token-а, подписанного legacy key, и retirement legacy signing key по `RetireAtUtc`
- signing key audit/reporting покрыт unit tests для sanitized lifecycle report и append-only audit snapshot serialization без key material
- `TOTP` protection key audit/reporting покрыт unit tests для sanitized lifecycle report по `key_version` usage и append-only audit snapshot serialization без key bytes
- maintenance workflow покрыт unit tests для `TOTP` re-encryption и cleanup/retention security-данных
- `TOTP` enrollment management теперь покрыт endpoint-level tests для `start/get/confirm/replace/revoke`, включая `401/403/409`, scope enforcement, `Location` header и отсутствие provisioning artifacts в read/revoke/confirm responses
