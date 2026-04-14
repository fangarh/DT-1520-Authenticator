# Backend

## Solution

- `OtpAuth.slnx`

## Projects

- `OtpAuth.Api` - HTTP API and integration endpoints
- `OtpAuth.Worker` - background jobs, outbox processing, delivery tasks
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

Operational maintenance is performed by a separate script and never on API startup.

Supported maintenance operations:

- `TOTP` secret re-encryption to the current protection key version
- cleanup of expired `totp_used_time_steps`
- cleanup of expired revoked access tokens
- cleanup of old `challenge_attempts`

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

If neither `BootstrapOAuth__CurrentSigningKey` nor legacy `BootstrapOAuth__SigningKey` is set, the API generates an ephemeral per-process signing key. That is acceptable only for local bootstrap work and invalidates issued tokens after restart.

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

## Current state

This workspace now contains a bootstrap `Challenges` vertical slice with `CreateChallenge`, `GetChallenge`, `VerifyTotp`, bootstrap OAuth, JWT bearer validation, token introspection/revocation, rotation-ready key rings for `TOTP` and signing keys, `PostgreSQL`-backed challenge storage, encrypted `TOTP` enrollment storage, persistent anti-replay, runtime rate limiting, security-data maintenance workflows, migrations and unit tests. It is still not production-ready.
