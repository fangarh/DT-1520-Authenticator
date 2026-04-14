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
- `seed-bootstrap-clients`
- `seed-bootstrap-totp-enrollment`
- `migrate-and-seed-bootstrap-clients`
- `reencrypt-totp-secrets`
- `cleanup-security-data`
- операционный скрипт: `backend/scripts/initialize-postgres.ps1`
- операционный maintenance-скрипт: `backend/scripts/maintain-security-data.ps1`
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

## Security constraints

- connection string хранится только в `ConnectionStrings__Postgres`
- bootstrap client secrets читаются только из env vars и не пишутся в репозиторий
- `Api` не выполняет auto-migrate и не сидирует secrets на старте
- все SQL-запросы параметризованы
- bootstrap client registry хранится как `client_secret_hash`, а не в открытом виде
- `Challenge` update выполняется с tenant/application scoping в `WHERE`, чтобы не допускать cross-scope overwrite
- `TOTP` secret хранится только в зашифрованном виде через `secret_ciphertext + nonce + tag + key_version`
- decrypt-path для `TOTP` должен поддерживать legacy keys, пока данные не переведены на новый current key
- verify-attempts фиксируются append-only в `auth.challenge_attempts`
- anti-replay reservation фиксируется в `auth.totp_used_time_steps` через `insert ... on conflict do nothing`
- revoked integration access tokens фиксируются в `auth.revoked_integration_access_tokens`
- cleanup/retention выполняется отдельной maintenance-операцией, а не на startup path `Api`
- re-encryption `TOTP` secrets выполняется отдельной maintenance-операцией через current `TotpProtection` key ring
- при bootstrap-подключении к серверам без TLS или с проблемным Windows SSL handshake нужно явно задавать режим соединения, например `SSL Mode=Disable`, в `ConnectionStrings__Postgres`

## Следующие persistence slices

1. Добавить `Outbox` таблицы и worker-backed delivery persistence.
2. Добавить cleanup/retention стратегию для expired `Challenges`.
3. Добавить operational reporting и audit around signing/protection key rotation.
