# Persistence and Migrations Plan

## Статус

Draft

## Технологический baseline

- `PostgreSQL` как основной persistent store
- `Dapper` для доступа к данным
- `Mapperly` для compile-time mapping
- `FluentMigrator` для versioned schema changes

## Migration runner

- отдельный проект: `backend/OtpAuth.Migrations`
- команды:
  - `ensure-database`
  - `migrate`
  - `initialize`
  - `inspect-signing-key-lifecycle`
  - `audit-signing-key-lifecycle`
  - `list-signing-key-lifecycle-audit-events [limit]`
  - `inspect-totp-protection-key-lifecycle`
  - `audit-totp-protection-key-lifecycle`
  - `list-totp-protection-key-lifecycle-audit-events [limit]`
  - `list-security-audit-events [limit] [event-type-prefix]`
- `seed-bootstrap-clients`
- `seed-bootstrap-totp-enrollment`
- `migrate-and-seed-bootstrap-clients`
- `reencrypt-totp-secrets`
- `cleanup-security-data`
- `rotate-integration-client-secret <client-id>`
- `deactivate-integration-client <client-id>`
- `activate-integration-client <client-id>`
- операционный скрипт: `backend/scripts/initialize-postgres.ps1`
- операционный maintenance-скрипт: `backend/scripts/maintain-security-data.ps1`
- операционный lifecycle-скрипт: `backend/scripts/manage-integration-client.ps1`
- операционный inspection-скрипт: `backend/scripts/inspect-signing-key-lifecycle.ps1`
- операционный audit/reporting-скрипт: `backend/scripts/audit-signing-key-lifecycle.ps1`
- операционные `TOTP` protection key inspection/audit-скрипты: `backend/scripts/inspect-totp-protection-key-lifecycle.ps1`, `backend/scripts/audit-totp-protection-key-lifecycle.ps1`
- скрипт сначала выполняет последовательную сборку migration runner-а, затем запускает команды через `--no-build`

## Текущее физическое покрытие

### Схема `auth`

#### `auth.integration_clients`

- `client_id` `varchar(200)` `PK`
- `tenant_id` `uuid`
- `application_client_id` `uuid`
- `client_secret_hash` `varchar(1024)`
- `is_active` `boolean`
- `created_utc` `timestamptz`
- `updated_utc` `timestamptz`
- `last_secret_rotated_utc` `timestamptz?`
- `last_auth_state_changed_utc` `timestamptz`

#### `auth.integration_client_scopes`

- `client_id` `varchar(200)` `FK -> auth.integration_clients.client_id`
- `scope` `varchar(200)`
- `PK(client_id, scope)`

#### `auth.challenges`

- `id` `uuid` `PK`
- `tenant_id` `uuid`
- `application_client_id` `uuid`
- `external_user_id` `varchar(256)`
- `username` `varchar(256)?`
- `operation_type` `integer`
- `operation_display_name` `varchar(256)?`
- `factor_type` `integer`
- `status` `integer`
- `expires_at` `timestamptz`
- `correlation_id` `varchar(128)?`
- `callback_url` `varchar(2048)?`
- `created_utc` `timestamptz`
- `updated_utc` `timestamptz`

#### `auth.totp_enrollments`

- `id` `uuid` `PK`
- `tenant_id` `uuid`
- `application_client_id` `uuid`
- `external_user_id` `varchar(256)`
- `username` `varchar(256)?`
- `secret_ciphertext` `bytea`
- `secret_nonce` `bytea`
- `secret_tag` `bytea`
- `key_version` `integer`
- `digits` `integer`
- `period_seconds` `integer`
- `algorithm` `varchar(32)`
- `is_active` `boolean`
- `confirmed_utc` `timestamptz?`
- `failed_confirm_attempts` `integer`
- `replacement_secret_ciphertext` `bytea?`
- `replacement_secret_nonce` `bytea?`
- `replacement_secret_tag` `bytea?`
- `replacement_key_version` `integer?`
- `replacement_digits` `integer?`
- `replacement_period_seconds` `integer?`
- `replacement_algorithm` `varchar(32)?`
- `replacement_started_utc` `timestamptz?`
- `replacement_failed_confirm_attempts` `integer`
- `last_used_utc` `timestamptz?`
- `created_utc` `timestamptz`
- `updated_utc` `timestamptz`

#### `auth.challenge_attempts`

- `id` `uuid` `PK`
- `challenge_id` `uuid` `FK -> auth.challenges.id`
- `attempt_type` `varchar(64)`
- `result` `varchar(64)`
- `created_utc` `timestamptz`

#### `auth.totp_used_time_steps`

- `enrollment_id` `uuid` `FK -> auth.totp_enrollments.id`
- `time_step` `bigint`
- `used_utc` `timestamptz`
- `expires_utc` `timestamptz`
- `PK(enrollment_id, time_step)`

#### `auth.revoked_integration_access_tokens`

- `jwt_id` `varchar(64)` `PK`
- `client_id` `varchar(200)`
- `revoked_utc` `timestamptz`
- `expires_utc` `timestamptz`
- `reason` `varchar(128)?`

#### `auth.signing_key_audit_events`

- `id` `uuid` `PK`
- `event_type` `varchar(128)`
- `current_key_id` `varchar(200)`
- `active_legacy_key_count` `integer`
- `retired_legacy_key_count` `integer`
- `warning_count` `integer`
- `summary` `varchar(512)`
- `payload_json` `jsonb`
- `created_utc` `timestamptz`

#### `auth.totp_protection_key_audit_events`

- `id` `uuid` `PK`
- `event_type` `varchar(128)`
- `current_key_version` `integer`
- `enrollments_requiring_reencryption_count` `integer`
- `warning_count` `integer`
- `summary` `varchar(512)`
- `payload_json` `jsonb`
- `created_utc` `timestamptz`

#### `auth.security_audit_events`

- `id` `uuid` `PK`
- `event_type` `varchar(128)`
- `subject_type` `varchar(64)`
- `subject_id` `varchar(256)?`
- `summary` `varchar(512)`
- `payload_json` `jsonb`
- `severity` `varchar(32)`
- `source` `varchar(64)`
- `created_utc` `timestamptz`

## Security constraints

- connection string хранится только в `ConnectionStrings__Postgres`
- bootstrap client secrets читаются только из env vars и не пишутся в репозиторий
- `Api` не выполняет auto-migrate и не сидирует secrets на старте
- все SQL-запросы параметризованы
- bootstrap client registry хранится как `client_secret_hash`, а не в открытом виде
- lifecycle integration client фиксируется через `last_auth_state_changed_utc`, чтобы rotate/deactivate/reactivate инвалидировали уже выданные JWT
- `Challenge` update выполняется с tenant/application scoping в `WHERE`, чтобы не допускать cross-scope overwrite
- `TOTP` secret хранится только в зашифрованном виде через `secret_ciphertext + nonce + tag + key_version`
- pending replacement `TOTP` secret тоже хранится только в зашифрованном виде через отдельные replacement-колонки и не должен подменять active secret до успешного confirm
- decrypt-path для `TOTP` должен поддерживать legacy keys, пока данные не переведены на новый current key
- verify-attempts фиксируются append-only в `auth.challenge_attempts`
- anti-replay reservation фиксируется в `auth.totp_used_time_steps` через `insert ... on conflict do nothing`
- revoked integration access tokens фиксируются в `auth.revoked_integration_access_tokens`
- cleanup/retention выполняется отдельной maintenance-операцией, а не на startup path `Api`
- re-encryption `TOTP` secrets выполняется отдельной maintenance-операцией через current `TotpProtection` key ring
- signing keys не хранятся в `PostgreSQL`; rollout/retirement остаются config-driven через secret manager/env
- legacy signing key должен иметь `RetireAtUtc`, иначе он остается валидным до явного удаления из конфигурации
- inspection lifecycle signing keys выполняется отдельной operational командой и не требует доступа к БД
- audit snapshot signing key lifecycle хранит только sanitized metadata и не должен содержать signing material
- audit snapshot `TOTP` protection key lifecycle хранит только metadata по `key_version`/backlog и не должен содержать key bytes или ciphertext
- unified `auth.security_audit_events` является каноническим append-only trail для security lifecycle/reporting
- integration client lifecycle audit пишет только metadata операции и не должен содержать `client_secret` или `client_secret_hash`
- при bootstrap-подключении к серверам без TLS или с проблемным Windows SSL handshake нужно явно задавать режим соединения, например `SSL Mode=Disable`, в `ConnectionStrings__Postgres`

## Следующие persistence slices

1. Добавить `Outbox` таблицы и worker-backed delivery persistence.
2. Добавить cleanup/retention стратегию для expired `Challenges`.
3. Расширить unified security audit trail от signing/`TOTP`/integration/enrollment lifecycle к token issuance, device activation и admin operations.
4. Добавить admin/API contour поверх operational integration client lifecycle.
