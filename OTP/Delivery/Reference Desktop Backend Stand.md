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

- current gate scope is `challenges:read challenges:write`; do not request `devices:read` or another device-routing scope until the stand explicitly enables target-device selection.
- `ReferenceBackend__CallbackUrlPolicyMode` defaults to `PublicInternet`: external `HTTPS`, no `localhost`, no loopback/private IP literal and no credential-bearing URL.
- `ReferenceBackend__CallbackUrlPolicyMode=PrivateNetwork` is allowed for closed HTTPS contours; `AllowInsecureCallbackHttp=true` is an explicit demo/on-prem relaxation and must stay visible in readiness/preflight output.
- `ReferenceBackend__CallbackUrlPolicyMode=LocalDevelopment` is allowed only for local/demo HTTP callback tests.
- desktop shell keeps only reference backend URL.
- `DesktopWpfTest` stores demo/live wiring values in encrypted JSON under LocalAppData for MVP convenience only; it is not a production secret store.
- `TOTP` codes are transient UI input and are not persisted.
- real secrets stay in environment variables or ignored local settings.
- next live execution must use Admin UI QR onboarding for the Android device and then run push approve/deny plus online `TOTP` fallback.

Preferred server-owned `ghostring` contour:

- `infra/docker/reference-backend.Dockerfile` builds `rdb_stand/src/ReferenceBackend`.
- `infra/docker-compose.ghostring.yml` includes `reference-backend` on the runtime network and exposes it only on host loopback `127.0.0.1:${REFERENCE_BACKEND_HOST_HTTP_PORT:-15188}`.
- `infra/nginx/reference-backend.ghostring.ru.conf.example` publishes `https://admin.ghostring.ru:18444/` through host `nginx`.
- `ReferenceBackend:CallbackUrl` should be `https://admin.ghostring.ru:18444/api/reference/callbacks/dt1520`.
- `RDB_BACKEND_BASE_URL` for `DesktopShell`/WPF should be `https://admin.ghostring.ru:18444/`.
- `Dt1520Authenticator:BaseUrl` inside the server-side reference service should normally be `http://api:8080/` to stay on the private compose network.
- `Dt1520Authenticator:Scope` should stay least-privilege `challenges:read challenges:write` unless explicit device routing is added.

## Latest Live Diagnostics

`2026-04-28` `Final Integrated Verification Gate` status:

- Android MCP sees `emulator-5554` online; a fresh APK launches to `MainActivity` and shows `Device onboarding`.
- Backend, admin, docs, rdb_stand and Android automated verification passed before live operation troubleshooting.
- `ReferenceBackend --preflight` is ready with local ignored config and a public callback URL, but direct live operation returns `502` because Windows/.NET fails the TLS transport to `https://admin.ghostring.ru:18443` before any HTTP response.
- Node/OpenSSL from the same workstation reaches `https://admin.ghostring.ru:18443/health/api` and receives expected token endpoint responses, so ghostring is reachable; this is a Windows SChannel contour issue.
- A loopback-only diagnostic proxy surfaced the next live blockers: current local `Dt1520Authenticator:Scope` includes `devices:read`, which the selected live integration client does not allow, and the deployed token endpoint still returns camelCase token fields until the backend snake_case fix is deployed.
- After server redeploy `b91f590`, local scope was changed to `challenges:read challenges:write` and `POST /api/reference/operations` succeeds through the loopback-only Node/OpenSSL proxy with an accepted `Waiting` session.
- Android MCP still shows an empty pending inbox on `emulator-5554`; third-party tunnel services are rejected for the target contour.
- Repo now contains server-owned `ghostring` assets for ReferenceBackend on `https://admin.ghostring.ru:18444/`; deploy this contour before retrying terminal callback/polling flow.

The live end-to-end flow is therefore blocked on deploying the server-owned `reference-backend` service/nginx site, confirming or redoing QR device activation for the current emulator, then retrying push approve/deny plus online `TOTP` fallback through `https://admin.ghostring.ru:18444/`.

`2026-04-29` server-owned `ghostring` contour status:

- Server report confirmed `reference-backend` image `sha256:e4e9cc50...`, public `https://admin.ghostring.ru:18444/health` healthy, live-readiness ready, callback URL `https://admin.ghostring.ru:18444/api/reference/callbacks/dt1520`, internal DT-1520 base URL `http://api:8080/` and scope `challenges:read challenges:write`.
- Local Node/OpenSSL confirmed public `health` and `live-readiness`; PowerShell/Windows SChannel still fails before HTTP response, matching the known local TLS client limitation.
- Android MCP confirmed `emulator-5554` online, `ru.dt1520.security.authenticator` installed/launched and empty pending inbox visible.
- First live `POST /api/reference/operations` through `https://admin.ghostring.ru:18444/` reached ReferenceBackend but returned `502` with SDK validation title `Request path must stay under the configured DT-1520 base URL.`
- Root cause: SDK URI guard treated rooted API paths as platform-dependent absolute URIs in Linux containers before combining them with the configured DT-1520 base URL.
- Fix added in `lib/src/Dt1520.Authenticator.Client/Dt1520AuthenticatorHttpPipeline.cs`: only absolute `http/https` URIs are accepted as absolute request targets; rooted API paths are combined with the validated base URL. Regression coverage added in `lib/tests/Dt1520.Authenticator.Client.Tests/ClientHttpFoundationTests.cs`.
- Verification after fix: `dotnet test .\lib\Dt1520.Authenticator.slnx --no-build -maxcpucount:1` green (`88/88`), `dotnet build .\rdb_stand\ReferenceDesktopBackendStand.slnx --no-restore -maxcpucount:1` green, `dotnet test .\rdb_stand\ReferenceDesktopBackendStand.slnx --no-build -maxcpucount:1` green (`28/28`).

Redeploy result with SDK URI guard fix:

- `reference-backend` redeployed from commit `cb259f5` as image `sha256:5a2acf76161e...`; public `https://admin.ghostring.ru:18444/health` returns `{"status":"healthy"}`.
- Live readiness is ready: `isReadyForLiveRun=true`, `configurationIssues=[]`, callback policy `PublicInternet`, `allowInsecureCallbackHttp=false`.
- Effective server-owned contour stayed aligned: callback URL `https://admin.ghostring.ru:18444/api/reference/callbacks/dt1520`, internal DT-1520 base URL `http://api:8080/`, scope `challenges:read challenges:write`.
- Compose recreated only `reference-backend`; `api`, `worker`, `admin` and `redis` container IDs stayed unchanged. `api` build layers were evaluated from cache by Compose because it is a dependency, but the running `api` container was not recreated.
- Operational caveat: `/opt/dt-1520-authenticator/runtime.env` currently lacks `REFERENCE_BACKEND_*` interpolation variables, so this redeploy preserved the effective reference-backend env from the previous running container without printing values. Persisting those variables into the server env file remains a reproducibility cleanup before the next unattended compose run.

Post-redeploy live retry:

- Local retry through `https://admin.ghostring.ru:18444/` confirmed the SDK URI guard fix: `POST /api/reference/operations` returned `202 Accepted` and created a `Waiting` reference session.
- Android MCP on `emulator-5554` still showed an empty pending inbox after foreground restart; the reference session stayed `Waiting` with no callback/terminal timestamp.

The active blocker moved from ReferenceBackend/SDK transport to Android device activation or push routing for the target `externalUserId`. Next step is to confirm or redo QR onboarding for the current emulator against the live runtime URL, then create a fresh reference operation and verify Android pending approve/deny plus online `TOTP` fallback.

Push-capable activation fix:

- Live server diagnostics showed the successfully activated phone device is `Active` but `isPushCapable=false`, so `CreateChallenge` correctly cannot bind a push challenge to that device and the reference session remains `Waiting`.
- Android debug builds now resolve a stable non-secret development push token from the encrypted local installation id during QR activation and pass it to `/api/v1/devices/activate-onboarding`.
- Release builds still send no synthetic push token; production FCM token wiring remains a separate productization task.
- Verification: `mobile :app:testDebugUnitTest :app:assembleDebug` passed outside sandbox with Android Studio JBR after `%USERPROFILE%\.gradle` lock `Access denied`; MCP installed the fresh debug APK on `emulator-5554` and launched `MainActivity`.
- Next live step: install the fresh `mobile/app/build/outputs/apk/debug/app-debug.apk` on the physical test phone, revoke/recreate QR if needed, activate once, confirm Admin UI shows the new device as `isPushCapable=true`, then retry reference approve/deny and online `TOTP` fallback.

Explicit TOTP fallback fix:

- After live push approve/deny proof, online `TOTP` fallback exposed the expected contract gap: the reference session had a push-selected primary challenge, so `verify-totp` against that challenge returned `409`.
- `ReferenceBackend` now creates a separate `Totp`-only fallback challenge for the same reference session and verifies the submitted code against that fallback challenge id.
- The in-memory session stores both primary challenge id and fallback `Totp` challenge id, plus fallback challenge request/create timestamps for status latency output.
- The first terminal reference-session state is immutable; fallback is rejected once the session is no longer waiting.
- WPF/desktop endpoint shape is unchanged; WPF default scope is aligned to `challenges:read challenges:write`.
- Verification is green: `ReferenceBackend.Tests 20/20`, `DesktopWpfTest.Tests 11/11`, full `rdb_stand` solution test passes. Live redeploy/proof remains the next gate.

Final gate preflight after combined QR consume implementation:

- Node/OpenSSL confirmed `ghostring` public health on `18443` and `18444`; live readiness is `isReadyForLiveRun=true` with no configuration issues.
- Playwright/Chromium gets `ERR_CONNECTION_RESET` on the same public TLS ports, while Node/OpenSSL succeeds. For this workstation, use Node/OpenSSL or server-side checks for health/readiness until the browser TLS path is understood.
- Android MCP currently exposes only `emulator-5554`; the physical Android phone required by the original gate prerequisite is not visible in this session.
- Fresh debug APK was installed on `emulator-5554` and launched successfully.
- `POST /api/reference/operations` for canonical pilot user `f1d6afaa-8a5d-4fd3-9f75-0a5c0177df81` returned `202 Accepted`, session `ef6c9c62448a4804957c1558e1c2122b`, status `Waiting`, with challenge creation timestamp `2026-04-29T12:26:58.7912289Z`.
- Android foreground polling did not surface a pending push; the app still showed an empty pending inbox and the reference session stayed `Waiting`.
- Next unblock remains fresh combined QR onboarding for the current Android runtime, then retry push approve, push deny and explicit online `TOTP` fallback. This session does not have admin credentials/env to create the live combined QR through Admin UI, and browser MCP cannot drive the Admin UI because of the TLS reset.

Server update after preflight:

- `ghostring` runtime is updated to commit `634fdfc`.
- Images were rebuilt and services `redis`, `api`, `worker`, `admin` and `reference-backend` were recreated.
- Migrations completed successfully.
- `ReferenceBackend` remains ready with internal DT-1520 base URL `http://api:8080/`, scope `challenges:read challenges:write`, callback URL `https://admin.ghostring.ru:18444/api/reference/callbacks/dt1520`, `PublicInternet` callback policy and `AllowInsecureCallbackHttp=false`.
- Server-side deployment is no longer blocking the final gate.
- Operator confirmed explicit online `TOTP` fallback reached terminal `Approved` after submitting fallback on a waiting reference session. This closes the fallback contract proof; final gate still needs the latest push approve/deny status and latency capture if those were not repeated after the full server update.

## Productization Direction

The reference backend is now the proving ground for the optional boxed `Integration Gateway`.

The target product shape is not a third-party tunnel or a desktop-only demo. It is a customer-owned deployable gateway that can safely hold backend integration credentials, validate callbacks, expose desktop/legacy polling endpoints and provide online `TOTP` fallback while keeping customer business commits in the integrating application.

`RDP + TOTP` is intentionally kept as a separate future access connector track. Gateway and SDK changes must preserve reusable lower-level services for that track, especially DT-1520 client calls, challenge creation, `TOTP` verification, callback validation, audit and identity mapping. Do not introduce desktop polling assumptions into those shared services.

## Latest Ghostring Runtime Alignment

`2026-04-28` runtime alignment work:

- `OtpAuth.Api` token response contract now explicitly serializes OAuth fields as `access_token`, `token_type`, `expires_in` and `scope`.
- `ReferenceBackend` sample configuration and runbook now request only `challenges:read challenges:write` for the current gate.
- Targeted backend contract test `IssueIntegrationTokenResponseTests` passes in a `.NET 10` Docker SDK container.
- Ghostring public token check returns `HTTP 200`, `token_type=Bearer`, `expires_in=3600` and granted scope `challenges:read challenges:write` without printing the access token.

## Related notes

- [[Official Dotnet Integration SDK]]
- [[Optional Boxed Integration Gateway]]
- [[QR Device Onboarding Follow-Up]]
- [[Push Delivery Latency Follow-Up]]
- [[Documentation Handoff Plan]]
- [[../Decisions/ADR-035 - Official Dotnet Integration SDK and Reference Stand]]
- [[../Decisions/ADR-036 - Optional Boxed Integration Gateway and Access Connector Boundary]]
