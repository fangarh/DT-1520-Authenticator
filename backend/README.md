# Backend

## Solution

- `OtpAuth.slnx`

## Projects

- `OtpAuth.Api` - HTTP API and integration endpoints
- `OtpAuth.Worker` - background jobs, outbox processing, delivery tasks
- `OtpAuth.Worker.Tests` - unit tests for worker diagnostics and heartbeat publishing
- `OtpAuth.Application` - application services and use cases
- `OtpAuth.Domain` - domain model and core business rules
- `OtpAuth.Infrastructure` - persistence, integrations, and adapters

## Commands

```powershell
cd .\backend
dotnet restore .\OtpAuth.slnx
dotnet build .\OtpAuth.slnx
```

The solution is configured to avoid parallel build workers because this workspace hits intermittent file locking in `artifacts\obj` during multi-node MSBuild.

For a safe end-to-end verification run, prefer the sequential script:

```powershell
cd .\backend
powershell -ExecutionPolicy Bypass -File .\scripts\verify-backend.ps1
```

Do not run backend `build` and `test` in parallel in this workspace.

## Worker diagnostics

`OtpAuth.Worker` now emits a sanitized heartbeat file for runtime diagnostics.

Behavior:

- default heartbeat path: temp directory + `otpauth-worker/heartbeat.json`
- container runtime overrides it to `/tmp/otpauth-worker/heartbeat.json`
- heartbeat is updated every `30` seconds by default
- worker also runs dependency probes for `Postgres` and `Redis`
- worker also executes the first real scheduled background job `security_data_cleanup`
- payload contains only sanitized execution metadata: heartbeat timestamps, last execution window, outcome, consecutive failure count, process id, dependency statuses/failure kinds and per-job `jobStatuses` with last run metadata and sanitized metrics
- `docker compose` worker healthcheck treats the heartbeat as stale after `2` minutes

This gives the installer and recovery flow a real worker-level readiness signal plus a minimal domain-execution report without exposing secrets or requiring a public HTTP endpoint from the worker.

## PostgreSQL bootstrap

Backend runtime and OAuth client registry now require `PostgreSQL`.

Required environment variable:

- `ConnectionStrings__Postgres` - application connection string for `PostgreSQL`
- `TotpProtection__CurrentKey` - base64-encoded 32-byte key for encrypted `TOTP` secrets at rest
- `TotpProtection__CurrentKeyVersion` - current encryption key version for new `TOTP` writes

Notes:

- `initialize-postgres.ps1` now builds the migration runner once and then executes commands via `--no-build`, which avoids repeated `MSBuild` instability in this workspace.
- If the target `PostgreSQL` server does not support TLS or hits Windows SSL handshake issues, set the mode explicitly in the connection string, for example `...;SSL Mode=Disable`.
- For key rotation, keep the current key in `TotpProtection__CurrentKey` and add legacy keys via `TotpProtection__AdditionalKeys__{n}__KeyVersion` and `TotpProtection__AdditionalKeys__{n}__Key`.

Bootstrap schema commands:

```powershell
cd .\backend
powershell -ExecutionPolicy Bypass -File .\scripts\initialize-postgres.ps1
```

If you also want to seed bootstrap integration clients from `BootstrapOAuth` config:

```powershell
cd .\backend
$env:OTPAUTH_BOOTSTRAP_CLIENT_SECRET = '<set-a-strong-secret>'
powershell -ExecutionPolicy Bypass -File .\scripts\initialize-postgres.ps1 -SeedBootstrapClients
```

If you also want to seed a bootstrap `TOTP` enrollment:

```powershell
cd .\backend
$env:TotpProtection__CurrentKey = '<base64-32-byte-key>'
$env:OTPAUTH_BOOTSTRAP_TOTP_EXTERNAL_USER_ID = 'user-123'
$env:OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64 = '<base64-encoded-totp-secret>'
powershell -ExecutionPolicy Bypass -File .\scripts\initialize-postgres.ps1 -SeedBootstrapTotpEnrollment
```

## Security data maintenance

Operational maintenance is never performed on API startup.

Supported maintenance operations:

- `TOTP` secret re-encryption to the current protection key version
- cleanup of expired `totp_used_time_steps`
- cleanup of expired revoked access tokens
- cleanup of old `challenge_attempts`

Runtime notes:

- `OtpAuth.Worker` now executes periodic `security_data_cleanup` and reports its progress through `jobStatuses`
- the maintenance script remains the on-demand path for explicit cleanup and `TOTP` re-encryption

Example:

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...'
$env:TotpProtection__CurrentKeyVersion = '2'
$env:TotpProtection__CurrentKey = '<base64-32-byte-key>'
powershell -ExecutionPolicy Bypass -File .\scripts\maintain-security-data.ps1 -ReEncryptTotpSecrets -CleanupSecurityData
```

Legacy keys for decrypt during re-encryption can be supplied via:

- `TotpProtection__AdditionalKeys__{n}__KeyVersion`
- `TotpProtection__AdditionalKeys__{n}__Key`

## Signing key lifecycle

Signing key rollout/retirement is config-driven and performed outside `Api`.

Current signing key:

- `BootstrapOAuth__CurrentSigningKeyId`
- `BootstrapOAuth__CurrentSigningKey`

Legacy signing keys:

- `BootstrapOAuth__AdditionalSigningKeys__{n}__KeyId`
- `BootstrapOAuth__AdditionalSigningKeys__{n}__Key`
- `BootstrapOAuth__AdditionalSigningKeys__{n}__RetireAtUtc`

Security behavior:

- new JWT access tokens are always signed by the current key
- legacy keys remain valid only until `RetireAtUtc`
- if a JWT contains `kid`, validation is fail-closed for unknown or retired keys
- ephemeral signing keys are allowed only in `Development`
- audit snapshots are append-only and store only sanitized metadata without signing material

Inspection command:

```powershell
cd .\backend
powershell -ExecutionPolicy Bypass -File .\scripts\inspect-signing-key-lifecycle.ps1
```

Audit/reporting commands:

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...'
powershell -ExecutionPolicy Bypass -File .\scripts\audit-signing-key-lifecycle.ps1
```

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...'
powershell -ExecutionPolicy Bypass -File .\scripts\audit-signing-key-lifecycle.ps1 -ListRecent -Limit 10
```

Signing key lifecycle events are now written to the unified append-only trail `auth.security_audit_events`.

Recommended rollout:

1. Generate a new signing key outside the repository.
2. Deploy it as `BootstrapOAuth__CurrentSigningKeyId` and `BootstrapOAuth__CurrentSigningKey`.
3. Move the previous current key into `BootstrapOAuth__AdditionalSigningKeys__{n}` and set `RetireAtUtc` to rollout time + access token lifetime + clock skew.
4. After `RetireAtUtc` passes, remove the retired key from configuration and redeploy.

## TOTP protection key lifecycle

`TOTP` protection key reporting is based on configured `key_version` metadata and enrollment usage in `PostgreSQL`.

Inspection command:

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Disable'
$env:TotpProtection__CurrentKeyVersion = '2'
powershell -ExecutionPolicy Bypass -File .\scripts\inspect-totp-protection-key-lifecycle.ps1
```

Audit/reporting commands:

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Disable'
$env:TotpProtection__CurrentKeyVersion = '2'
powershell -ExecutionPolicy Bypass -File .\scripts\audit-totp-protection-key-lifecycle.ps1
```

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Disable'
powershell -ExecutionPolicy Bypass -File .\scripts\audit-totp-protection-key-lifecycle.ps1 -ListRecent -Limit 10
```

Security behavior:

- inspection/audit use only configured `key_version` metadata and database usage
- audit snapshots are append-only and store only sanitized metadata without key bytes or ciphertext
- legacy `TOTP` protection key versions should remain in runtime only until re-encryption backlog reaches zero
- `TOTP` protection key lifecycle events are stored in the unified append-only trail `auth.security_audit_events`

## Integration client lifecycle operations

Operational integration client management is performed outside `Api`.

Supported operations:

- rotate bootstrap client secret
- deactivate integration client
- reactivate integration client

Security behavior:

- lifecycle operations update persisted auth state in `PostgreSQL`
- issued JWT access tokens become invalid when their `iat` is older than the current client auth state
- this means secret rotation and deactivate/reactivate do not rely only on future token issuance
- each lifecycle operation writes a sanitized event into `auth.security_audit_events`
- audit payload stores only operation metadata and never includes `client_secret` or `client_secret_hash`

Examples:

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...'
powershell -ExecutionPolicy Bypass -File .\scripts\manage-integration-client.ps1 -ClientId otpauth-crm -Deactivate
```

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...'
powershell -ExecutionPolicy Bypass -File .\scripts\manage-integration-client.ps1 -ClientId otpauth-crm -RotateSecret
```

If you need to provide an explicit new secret instead of generating one:

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...'
powershell -ExecutionPolicy Bypass -File .\scripts\manage-integration-client.ps1 -ClientId otpauth-crm -RotateSecret -NewClientSecret '<strong-secret>'
```

## Unified security audit trail

Security lifecycle reporting is now aggregated in `auth.security_audit_events`.

Examples:

```powershell
cd .\
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Disable'
dotnet run --project .\backend\OtpAuth.Migrations\OtpAuth.Migrations.csproj -- list-security-audit-events 10
```

```powershell
cd .\
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Disable'
dotnet run --project .\backend\OtpAuth.Migrations\OtpAuth.Migrations.csproj -- list-security-audit-events 10 integration_client_lifecycle.
```

Security behavior:

- the unified trail is append-only
- lifecycle payloads are sanitized and exclude signing keys, `TOTP` key bytes, ciphertext, `client_secret`, and `client_secret_hash`
- signing and `TOTP` legacy lifecycle tables remain legacy/backfill sources and are no longer the canonical reporting path

## Bootstrap OAuth for local development

Integration auth now uses bootstrap `OAuth 2.0 client credentials` with `/oauth2/token` and reads clients from `PostgreSQL`.

Required environment variable:

- `OTPAUTH_BOOTSTRAP_CLIENT_SECRET` - client secret for the bootstrap integration client

Optional environment variable:

- `BootstrapOAuth__CurrentSigningKeyId` - `kid` for new access tokens
- `BootstrapOAuth__CurrentSigningKey` - symmetric signing key for new access tokens

Optional legacy signing keys:

- `BootstrapOAuth__AdditionalSigningKeys__{n}__KeyId`
- `BootstrapOAuth__AdditionalSigningKeys__{n}__Key`
- `BootstrapOAuth__AdditionalSigningKeys__{n}__RetireAtUtc`

If neither `BootstrapOAuth__CurrentSigningKey` nor legacy `BootstrapOAuth__SigningKey` is set, the API generates an ephemeral per-process signing key only in `Development`. That is acceptable only for local bootstrap work and invalidates issued tokens after restart.

Bootstrap client metadata from `appsettings.Development.json`:

- client id: `otpauth-crm`
- tenant id: `6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb`
- application client id: `f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4`
- allowed scopes: `challenges:read`, `challenges:write`

Bootstrap OAuth now also supports:

- `POST /oauth2/introspect`
- `POST /oauth2/revoke`

Both endpoints require `application/x-www-form-urlencoded` with the same `client_id` and `client_secret` authentication model as `/oauth2/token`.

Example token request:

```powershell
curl.exe -X POST http://localhost:5143/oauth2/token ^
  -H "Content-Type: application/x-www-form-urlencoded" ^
  -d "grant_type=client_credentials&client_id=otpauth-crm&client_secret=$env:OTPAUTH_BOOTSTRAP_CLIENT_SECRET&scope=challenges:read challenges:write"
```

Note: if the client secret contains `+`, `/` or `=`, URL-encode it when sending `application/x-www-form-urlencoded`.

## Admin UI bootstrap for local development

`Admin UI MVP` uses a separate cookie-based admin contour backed by `auth.admin_users`.

Operational commands:

- `list-admin-users` - prints sanitized bootstrap admin users and permissions
- `upsert-admin-user <username> <permission> [permission...]` - creates or updates an active bootstrap admin user

Required environment variable for `upsert-admin-user`:

- `OTPAUTH_ADMIN_PASSWORD` - plaintext bootstrap password used only to produce a PBKDF2 hash during the operation

Supported bootstrap permissions:

- `enrollments.read`
- `enrollments.write`

Example:

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Disable'
$env:OTPAUTH_ADMIN_PASSWORD = '<set-a-strong-password>'
dotnet run --project .\OtpAuth.Migrations\OtpAuth.Migrations.csproj -- upsert-admin-user operator enrollments.read enrollments.write
```

```powershell
cd .\backend
$env:ConnectionStrings__Postgres = 'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Disable'
dotnet run --project .\OtpAuth.Migrations\OtpAuth.Migrations.csproj -- list-admin-users
```

Security notes:

- bootstrap password is never accepted as a CLI argument and must come from `OTPAUTH_ADMIN_PASSWORD`
- printed output is sanitized and does not include password hashes
- unsupported permissions fail closed

## Current state

This workspace now contains a bootstrap `Challenges` vertical slice with `CreateChallenge`, `GetChallenge`, `VerifyTotp`, bootstrap OAuth, JWT bearer validation, token introspection/revocation, operational integration client lifecycle, config-driven signing key rollout/retirement, a unified append-only security audit trail, rotation-ready key rings for `TOTP` and signing keys, `PostgreSQL`-backed challenge storage, encrypted `TOTP` enrollment storage, persistent anti-replay, runtime rate limiting, security-data maintenance workflows, migrations and unit tests. It is still not production-ready.
