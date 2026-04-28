# Reference Desktop Backend Stand

## Status

Local live wiring handoff completed. Next continuation point is executing the runbook against real `DT-1520` backend/worker plus QR-activated Android device and recording latency.

The SDK handoff entry points are:

- `lib/PRERELEASE-HANDOFF.md`
- `rdb_stand/README.md`

Current code entry points:

- `rdb_stand/ReferenceDesktopBackendStand.slnx`
- `rdb_stand/src/ReferenceBackend`
- `rdb_stand/src/DesktopShell`
- `rdb_stand/tests/ReferenceBackend.Tests`
- `rdb_stand/src/ReferenceBackend/appsettings.Development.example.json`

## Goal

Создать минимальный воспроизводимый стенд `Desktop + Backend`, который проверяет интеграцию с `DT-1520 Authenticator` без шума `ProjectManager`, `Keycloak` и production-like business workflow.

Фактический repo root для стенда: `rdb_stand/`.

Этот стенд становится основным контуром для повторного цикла:

- NuGet SDK validation
- Android QR onboarding validation
- push approval latency measurement
- online `TOTP` fallback validation
- documentation and handoff examples

## Target topology

`Desktop App -> Reference Backend -> DT-1520 Authenticator -> Android App`

Desktop app:

- не хранит `integration client_secret`
- не вызывает `DT-1520` напрямую в default flow
- показывает pending approval UX
- умеет fallback на ввод `TOTP` кода

Reference backend:

- хранит integration client configuration
- вызывает `DT-1520` через `.NET` SDK
- создает challenge
- принимает signed callback or uses controlled polling fallback
- применяет result only after `approved`
- проверяет online `TOTP` code через `DT-1520`

Android:

- активируется через QR onboarding
- подтверждает push approval
- генерирует offline `TOTP` code локально

## Latency measurement target

`ProjectManager` больше не является основным diagnostic contour для `~60s` lag.

Reference stand должен измерять:

- desktop submit timestamp
- backend create-challenge timestamp
- `auth.challenges` creation timestamp
- `auth.push_challenge_deliveries` enqueue timestamp
- worker processing timestamp
- pending inbox visibility timestamp
- Android UI visible timestamp
- approve/deny terminal timestamp

Pilot acceptable target:

- opened Android app sees pending push in `5-10s`
- if app is backgrounded and real push provider is not configured, limitation is documented explicitly

## Online TOTP fallback

Под offline-code fallback в этом track понимается:

- Android генерирует `TOTP` без сети
- Desktop user вводит код в стенде
- Backend остается online
- Backend вызывает `DT-1520` verify path
- centralized policy, replay defense, audit and rate limiting remain active

Не входит в scope:

- backend-local verifier cache
- login while `DT-1520` runtime is unavailable
- bypassing centralized audit/revoke/rate-limit controls

## First implementation expectations

- simplest console desktop shell: done
- simplest backend API: done
- SDK-based integration through local project references: done
- QR-based Android activation: runbook prepared, execution next
- push approval flow: backend/desktop contract done, live Android path next
- `TOTP` fallback flow: backend endpoint and tests done
- latency instrumentation: baseline timestamps done; delivery/inbox/Android timestamps next during live run
- live wiring handoff: env-var runbook, ignored local settings and sanitized readiness endpoint done
- local preflight command: `dotnet run --no-build --project .\src\ReferenceBackend\ReferenceBackend.csproj -- --preflight` done
- Visual Studio solution restore: SDK projects from `../lib/src` are included in `ReferenceDesktopBackendStand.slnx` to avoid `NU1105` for unloaded project references
- automated backend/SDK tests: `ReferenceBackend.Tests` done, including callback URL hardening
- UI verification: not required for console shell
- security review: done for secret boundary and callback validation

## Verification

```powershell
cd .\rdb_stand
dotnet restore .\ReferenceDesktopBackendStand.slnx
dotnet build .\ReferenceDesktopBackendStand.slnx --no-restore -maxcpucount:1
dotnet test .\ReferenceDesktopBackendStand.slnx --no-build -maxcpucount:1
dotnet run --no-build --project .\src\ReferenceBackend\ReferenceBackend.csproj -- --preflight
```

## Live Runbook

See `rdb_stand/README.md`.

Required local-only values:

- `Dt1520Authenticator__BaseUrl`
- `Dt1520Authenticator__ClientId`
- `Dt1520Authenticator__ClientSecret`
- `Dt1520Authenticator__CallbackSigningSecret`
- `ReferenceBackend__TenantId`
- `ReferenceBackend__ApplicationClientId`
- `ReferenceBackend__CallbackUrl`
- `RDB_BACKEND_BASE_URL`

Security constraints:

- `ReferenceBackend__CallbackUrl` must be external `HTTPS`, not `localhost`, private IP literal or credential-bearing URL.
- desktop shell keeps only reference backend URL.
- real secrets stay in environment variables or ignored local settings.
- next live execution must use Admin UI QR onboarding for the Android device and then run push approve/deny plus online `TOTP` fallback.

## Latest Local Preflight

`2026-04-28` local environment status:

- Android MCP sees `emulator-5554` online and the app package `ru.dt1520.security.authenticator` launches to `MainActivity`.
- `Device onboarding` UI is visible on the emulator.
- no `rdb_stand/src/ReferenceBackend/appsettings.Development.json` or `appsettings.Local.json` is present.
- required live env vars are not set in the current shell.
- `--preflight` returns not ready with missing `Dt1520Authenticator` and `ReferenceBackend` configuration issues.

The live end-to-end flow is therefore blocked on supplying backend-only live configuration and an external HTTPS callback URL, not on Android availability.

## Related notes

- [[Official Dotnet Integration SDK]]
- [[QR Device Onboarding Follow-Up]]
- [[Push Delivery Latency Follow-Up]]
- [[Documentation Handoff Plan]]
- [[../Decisions/ADR-035 - Official Dotnet Integration SDK and Reference Stand]]
