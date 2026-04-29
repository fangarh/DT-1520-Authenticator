# Mobile

Android app workspace lives here.

## Modules

- `:app` - composition root and Android entry point
- `:core:ui` - shared theme and section containers
- `:feature:device-onboarding` - QR activation payload validation/import workflow
- `:feature:provisioning` - future provisioning/import flow
- `:feature:totp-codes` - future offline TOTP runtime screen
- `:security:storage` - secure storage boundary for `Keystore`
- `:totp-domain` - pure Kotlin `TOTP` domain contracts

## Commands

```powershell
cd .\mobile
$env:JAVA_HOME='C:\Program Files\Android\Android Studio\jbr'
$env:Path="$env:JAVA_HOME\bin;$env:Path"
.\gradlew.bat :app:testDebugUnitTest :core:ui:assembleDebug :feature:provisioning:testDebugUnitTest :feature:totp-codes:testDebugUnitTest :security:storage:testDebugUnitTest :totp-domain:test
```

If `JAVA_HOME` is already configured, only the Gradle command is required.

## Gradle hang workaround

This workspace can hit `%USERPROFILE%\.gradle` wrapper/cache locks, especially after an interrupted test run.

When Gradle hangs or fails on a `.lck` file:

1. Check running Java/Gradle processes:

```powershell
Get-Process java, gradle -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,CPU,StartTime,Path
```

2. Stop only stale processes from the interrupted run.
3. Re-run with explicit Android Studio JBR:

```powershell
cd .\mobile
$env:JAVA_HOME='C:\Program Files\Android\Android Studio\jbr'
.\gradlew.bat <tasks>
```

If the failure is sandbox-only access to `%USERPROFILE%\.gradle`, repeat the same Gradle command outside the sandbox with approval.

## QR device onboarding

The production-oriented Android onboarding path is:

1. Operator creates a one-time QR artifact in Admin UI.
2. Android scans a v1 QR envelope `{ v, runtimeBaseUrl, activationPayload }`; legacy raw `dac_...` payloads are temporarily still accepted.
3. `:feature:device-onboarding` validates/imports the envelope or legacy payload, exposing `activationPayload` and nullable `runtimeBaseUrl`.
4. The app calls `POST /api/v1/devices/activate-onboarding` through the QR `runtimeBaseUrl` when present; legacy raw payloads temporarily fall back to `-PdeviceRuntimeBaseUrl`.
5. After successful activation, the QR runtime URL is stored with the encrypted device session and reused after app restart for refresh, pending push polling, approve and deny.
6. The backend derives tenant/application/user binding from the server-side artifact and atomically consumes it during activation.

Runtime URL parsing is fail-closed: only `https` URLs with a non-empty host and no embedded credentials/userinfo are accepted.
If a legacy raw payload is used without a configured runtime URL, activation fails with a generic user-facing message that the QR does not contain a runtime address.
Productized QR onboarding no longer requires building the APK with `-PdeviceRuntimeBaseUrl`; that flag remains only a temporary compatibility path for legacy raw `dac_...` payloads and debug tooling.

`mobile/app/src/debug/PilotDeviceActivationActivity.kt` remains debug-only pilot tooling and is not part of production onboarding handoff.
