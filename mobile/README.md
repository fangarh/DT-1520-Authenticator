# Mobile

Android app workspace lives here.

## Modules

- `:app` - composition root and Android entry point
- `:core:ui` - shared theme and section containers
- `:feature:provisioning` - future provisioning/import flow
- `:feature:totp-codes` - future offline TOTP runtime screen
- `:security:storage` - secure storage boundary for `Keystore`
- `:totp-domain` - pure Kotlin `TOTP` domain contracts

## Commands

```powershell
cd .\mobile
$env:JAVA_HOME='C:\Program Files\Android\openjdk\jdk-21.0.8'
$env:Path="$env:JAVA_HOME\bin;$env:Path"
.\gradlew.bat :app:testDebugUnitTest :core:ui:assembleDebug :feature:provisioning:testDebugUnitTest :feature:totp-codes:testDebugUnitTest :security:storage:testDebugUnitTest :totp-domain:test
```

If `JAVA_HOME` is already configured, only the Gradle command is required.
