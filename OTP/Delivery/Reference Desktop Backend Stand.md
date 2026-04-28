# Reference Desktop Backend Stand

## Status

Local live wiring handoff plus WPF MVP demo shell completed. Next continuation point is executing the runbook against real `DT-1520` backend/worker plus QR-activated Android device and recording latency.

The SDK handoff entry points are:

- `lib/PRERELEASE-HANDOFF.md`
- `rdb_stand/README.md`

Current code entry points:

- `rdb_stand/ReferenceDesktopBackendStand.slnx`
- `rdb_stand/src/ReferenceBackend`
- `rdb_stand/src/DesktopShell`
- `rdb_stand/src/DesktopWpfTest`
- `rdb_stand/tests/ReferenceBackend.Tests`
- `rdb_stand/tests/DesktopWpfTest.Tests`
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
- WPF MVVM desktop demo shell with encrypted local MVP settings: done
- simplest backend API: done
- SDK-based integration through local project references: done
- QR-based Android activation: runbook prepared, execution next
- push approval flow: backend/desktop contract done, live Android path next
- `TOTP` fallback flow: backend endpoint and tests done
- latency instrumentation: baseline timestamps done; delivery/inbox/Android timestamps next during live run
- live wiring handoff: env-var runbook, ignored local settings and sanitized readiness endpoint done
- local preflight command: `dotnet run --no-build --project .\src\ReferenceBackend\ReferenceBackend.csproj -- --preflight` done
- Visual Studio solution restore: SDK projects from `../lib/src` are included in `ReferenceDesktopBackendStand.slnx` to avoid `NU1105` for unloaded project references
- automated backend/SDK/WPF tests: `ReferenceBackend.Tests` and `DesktopWpfTest.Tests` done, including callback URL policy, encrypted JSON storage and transient `TOTP` input
- UI verification: not required for WPF desktop shell because it is not a browser surface
- security review: done for backend secret boundary, callback validation and WPF MVP/demo-only local encrypted JSON storage

## Verification

```powershell
cd .\rdb_stand
dotnet restore .\ReferenceDesktopBackendStand.slnx
dotnet build .\ReferenceDesktopBackendStand.slnx --no-restore -maxcpucount:1
dotnet test .\ReferenceDesktopBackendStand.slnx --no-build -maxcpucount:1
dotnet run --no-build --project .\src\ReferenceBackend\ReferenceBackend.csproj -- --preflight
dotnet run --project .\src\DesktopWpfTest\DesktopWpfTest.csproj
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

- `ReferenceBackend__CallbackUrlPolicyMode` defaults to `PublicInternet`: external `HTTPS`, no `localhost`, no loopback/private IP literal and no credential-bearing URL.
- `ReferenceBackend__CallbackUrlPolicyMode=PrivateNetwork` is allowed for closed HTTPS contours; `AllowInsecureCallbackHttp=true` is an explicit demo/on-prem relaxation and must stay visible in readiness/preflight output.
- `ReferenceBackend__CallbackUrlPolicyMode=LocalDevelopment` is allowed only for local/demo HTTP callback tests.
- desktop shell keeps only reference backend URL.
- `DesktopWpfTest` stores demo/live wiring values in encrypted JSON under LocalAppData for MVP convenience only; it is not a production secret store.
- `TOTP` codes are transient UI input and are not persisted.
- real secrets stay in environment variables or ignored local settings.
- next live execution must use Admin UI QR onboarding for the Android device and then run push approve/deny plus online `TOTP` fallback.

## Latest Live Diagnostics

`2026-04-28` `Final Integrated Verification Gate` status:

- Android MCP sees `emulator-5554` online; a fresh APK launches to `MainActivity` and shows `Device onboarding`.
- Backend, admin, docs, rdb_stand and Android automated verification passed before live operation troubleshooting.
- `ReferenceBackend --preflight` is ready with local ignored config and a public callback URL, but direct live operation returns `502` because Windows/.NET fails the TLS transport to `https://admin.ghostring.ru:18443` before any HTTP response.
- Node/OpenSSL from the same workstation reaches `https://admin.ghostring.ru:18443/health/api` and receives expected token endpoint responses, so ghostring is reachable; this is a Windows SChannel contour issue.
- A loopback-only diagnostic proxy surfaced the next live blockers: current local `Dt1520Authenticator:Scope` includes `devices:read`, which the selected live integration client does not allow, and the deployed token endpoint still returns camelCase token fields until the backend snake_case fix is deployed.

The live end-to-end flow is therefore blocked on redeploying the OAuth token response fix, aligning integration client scopes/config, and either fixing the Windows SChannel path or running `ReferenceBackend` from an OpenSSL/Linux contour.

## Related notes

- [[Official Dotnet Integration SDK]]
- [[QR Device Onboarding Follow-Up]]
- [[Push Delivery Latency Follow-Up]]
- [[Documentation Handoff Plan]]
- [[../Decisions/ADR-035 - Official Dotnet Integration SDK and Reference Stand]]
