# Mobile

Android app scaffold lives here.

## Commands

```powershell
cd .\mobile
$env:JAVA_HOME='C:\Program Files\Android\openjdk\jdk-21.0.8'
$env:Path="$env:JAVA_HOME\bin;$env:Path"
.\gradlew.bat :app:dependencies --configuration debugRuntimeClasspath
```

If `JAVA_HOME` is already configured, only the Gradle command is required.
