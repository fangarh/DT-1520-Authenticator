# Logical Data Model

## Основные сущности

### Tenant

Нужен для `multi-tenant` режима и сегментации клиентов интеграции.

Поля:

- `id`
- `name`
- `status`
- `deployment_mode`
- `created_at`

### ApplicationClient

Интегрируемая система, которая использует `2FA Server`.

Поля:

- `id`
- `tenant_id`
- `name`
- `client_type`
- `auth_method`
- `client_secret_hash`
- `redirect_uris`
- `allowed_factors`

### User

Пользователь, для которого выполняется второй фактор.

Поля:

- `id`
- `tenant_id`
- `external_subject_id`
- `username`
- `email`
- `phone`
- `status`

### FactorEnrollment

Связывает пользователя с конкретным фактором.

Поля:

- `id`
- `user_id`
- `factor_type`
- `status`
- `created_at`
- `confirmed_at`
- `last_used_at`

### TotpSecret

Параметры `TOTP`-секрета и его шифрования.

Поля:

- `enrollment_id`
- `secret_ciphertext`
- `secret_kek_version`
- `digits`
- `period_seconds`
- `algorithm`

### Device

Зарегистрированное мобильное устройство.

Поля:

- `id`
- `user_id`
- `platform`
- `device_name`
- `push_token`
- `public_key`
- `attestation_status`
- `last_seen_at`
- `revoked_at`

### Challenge

Основная сущность подтверждения второго фактора.

Поля:

- `id`
- `tenant_id`
- `application_client_id`
- `user_id`
- `operation_type`
- `factor_type`
- `status`
- `expires_at`
- `approved_at`
- `denied_at`
- `correlation_id`

### ChallengeAttempt

Все попытки взаимодействия с `challenge`.

Поля:

- `id`
- `challenge_id`
- `attempt_type`
- `result`
- `ip`
- `user_agent`
- `created_at`

### BackupCode

Разовые аварийные коды доступа.

Поля:

- `id`
- `user_id`
- `code_hash`
- `used_at`

### AuditEvent

Нерепутационный и расследовательский журнал безопасности.

Поля:

- `id`
- `tenant_id`
- `event_type`
- `actor_type`
- `actor_id`
- `subject_id`
- `severity`
- `payload_json`
- `created_at`

## Хранилища

### PostgreSQL

Постоянные данные:

- пользователи
- enrollments
- устройства
- клиенты интеграции
- аудит

### Redis

Краткоживущие данные:

- активные `challenge`
- anti-replay ключи
- rate limit counters
- session state

## Правила безопасности

- не хранить `OTP` и `backup codes` в открытом виде
- секреты `TOTP` хранить только зашифрованными
- ключи шифрования выносить в `Vault` или `KMS`
- критичные аудит-события должны быть немодифицируемыми на уровне процесса
