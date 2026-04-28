# Reference Desktop + Backend Stand

`rdb_stand/` is the reference productization track after the official `.NET` SDK prerelease closure.

## Current Slice

The first scaffold slice is implemented:

- `ReferenceDesktopBackendStand.slnx` is the stand solution entry point.
- The solution includes local SDK projects from `../lib/src` so Visual Studio restore can resolve stand `ProjectReference` dependencies without unloaded-project `NU1105` errors.
- `src/ReferenceBackend` is a minimal ASP.NET Core backend that consumes the local SDK projects.
- `src/DesktopShell` is a console desktop shell that talks only to the reference backend.
- `tests/ReferenceBackend.Tests` covers challenge start, signed callback validation, status polling state and online `TOTP` fallback.
- `src/ReferenceBackend/appsettings.Development.example.json` documents live wiring without storing real secrets.

Verification:

```powershell
cd .\rdb_stand
dotnet restore .\ReferenceDesktopBackendStand.slnx
dotnet build .\ReferenceDesktopBackendStand.slnx --no-restore -maxcpucount:1
dotnet test .\ReferenceDesktopBackendStand.slnx --no-build -maxcpucount:1
```

## Goal

Build a minimal reproducible stand for validating DT-1520 Authenticator without `ProjectManager`, `Keycloak` or production-like business workflow noise.

Default topology:

```text
Desktop App -> Reference Backend -> DT-1520 Authenticator -> Android App
```

## SDK Inputs

Consume the internal prerelease packages from `lib/`:

- `Dt1520.Authenticator.Client`
- `Dt1520.Authenticator.AspNetCore`
- `Dt1520.Authenticator.Desktop`

Expected SDK surfaces:

- `AddDt1520Authenticator(...)`
- `CreateChallengeAsync(...)`
- `GetChallengeAsync(...)`
- `VerifyTotpAsync(...)`
- `ListDevicesForRoutingAsync(...)`
- `SelectSinglePushDeviceAsync(...)`
- `Dt1520AuthenticatorCallbackValidator`
- `DesktopApprovalSession`
- `DesktopApprovalPoller`
- `DesktopApprovalOutcome`

## Reference Backend Scope

The backend currently:

- store DT-1520 integration client configuration server-side;
- create a pending protected operation draft before calling DT-1520;
- call `CreateChallengeAsync` for push or TOTP-capable challenge creation;
- validate callbacks through original request body bytes and `X-OTPAuth-Signature`;
- expose a desktop status endpoint that returns a `DesktopApprovalSession`-compatible JSON shape;
- commit the protected operation exactly once only after an approved challenge result;
- expose an online TOTP fallback endpoint that calls `VerifyTotpAsync`;
- record baseline latency timestamps for desktop submit, backend challenge request, challenge created, callback received, TOTP submitted and terminal decision.

Runtime configuration is split between backend-owned `Dt1520Authenticator` SDK settings and reference-stand settings:

```json
{
  "Dt1520Authenticator": {
    "BaseUrl": "https://auth.example.test/",
    "ClientId": "integration-client-id",
    "ClientSecret": "<backend-secret-store>",
    "CallbackSigningSecret": "<backend-secret-store>"
  },
  "ReferenceBackend": {
    "TenantId": "00000000-0000-0000-0000-000000000000",
    "ApplicationClientId": "00000000-0000-0000-0000-000000000000",
    "CallbackUrl": "https://reference-backend.example.test/api/reference/callbacks/dt1520"
  }
}
```

Do not commit `appsettings.Development.json`, `appsettings.Local.json`, `.env` files or real callback URLs that embed credentials. The repository ignores local `rdb_stand` development settings.

## Live Wiring

The reference backend must be reachable by DT-1520 for signed challenge callbacks. For a local run this usually means:

- run `ReferenceBackend` on `127.0.0.1`;
- expose it through a temporary HTTPS tunnel or a controlled reverse proxy;
- set `ReferenceBackend:CallbackUrl` to the externally reachable HTTPS callback URL ending in `/api/reference/callbacks/dt1520`;
- keep `Dt1520Authenticator:ClientSecret` and `Dt1520Authenticator:CallbackSigningSecret` in environment variables or a local untracked settings file.

PowerShell environment example:

```powershell
cd .\rdb_stand
$env:ASPNETCORE_URLS = "http://127.0.0.1:5188"
$env:Dt1520Authenticator__BaseUrl = "https://admin.ghostring.ru:18443/"
$env:Dt1520Authenticator__ClientId = "<integration-client-id>"
$env:Dt1520Authenticator__ClientSecret = "<backend-secret-store>"
$env:Dt1520Authenticator__CallbackSigningSecret = "<challenge-callback-signing-secret>"
$env:Dt1520Authenticator__Scope = "challenges:read challenges:write devices:read"
$env:ReferenceBackend__TenantId = "<tenant-guid>"
$env:ReferenceBackend__ApplicationClientId = "<application-client-guid>"
$env:ReferenceBackend__CallbackUrl = "https://<public-https-host>/api/reference/callbacks/dt1520"
dotnet run --project .\src\ReferenceBackend\ReferenceBackend.csproj
```

Readiness check:

```powershell
dotnet run --no-build --project .\src\ReferenceBackend\ReferenceBackend.csproj -- --preflight
Invoke-RestMethod http://127.0.0.1:5188/api/reference/live-readiness
```

`--preflight` can be run before starting the web host. It reports only booleans, host names and configuration issue names; it does not print `clientSecret`, callback signing secret, bearer tokens or callback payloads. Exit code `0` means the backend has enough configuration to start a live run; non-zero means configuration is incomplete.

Desktop shell:

```powershell
$env:RDB_BACKEND_BASE_URL = "http://127.0.0.1:5188/"
dotnet run --project .\src\DesktopShell\DesktopShell.csproj
```

Live run checklist:

1. Create or select an active integration client in Admin UI with `challenges:read`, `challenges:write` and `devices:read`.
2. Use Admin UI QR device onboarding to activate an Android device for the target `externalUserId`.
3. Start `ReferenceBackend` with backend-only DT-1520 credentials and a public HTTPS callback URL.
4. Start `DesktopShell`, enter the same `externalUserId`, then approve or deny the pending push in Android.
5. Repeat once with TOTP fallback: press Enter in `DesktopShell`, enter the Android-generated TOTP code, and confirm centralized verify succeeds.
6. Capture latency timestamps from the status response for desktop submit, backend challenge request, challenge creation, callback/TOTP submission and terminal state.

## Desktop Scope

The desktop shell currently:

- call only the reference backend;
- display pending, approved, denied, expired, failed, cancelled and timeout states;
- poll only relative backend status paths through `DesktopApprovalPoller`;
- offer TOTP fallback by sending the user-entered code to the backend;
- never hold DT-1520 integration credentials, bearer tokens, callback signing secrets or direct DT-1520 base URL configuration.

## Android Scope

The Android app should:

- activate through QR device onboarding;
- show pending push approval;
- approve or deny through the existing biometric-gated flow;
- generate offline TOTP codes for backend-online fallback verification.

## First Verification Gate

- backend unit tests for challenge start, callback validation, status polling and TOTP fallback are in place;
- desktop shell is console-based, so no Playwright/browser verification is required for this slice;
- SDK consumption uses local project references to the prerelease SDK projects in `lib/src`;
- security review: desktop has only reference backend URL, backend owns DT-1520 secrets, callback validation uses raw request body bytes, and non-approved outcomes do not commit;
- live wiring runbook and sanitized readiness endpoint are in place;
- `--preflight` is available for secret-safe config checks before starting the web host;
- next live slice should execute the runbook against real DT-1520 config, QR-activated Android device and running backend/worker/mobile runtime.
