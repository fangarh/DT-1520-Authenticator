# OTPAuth Workspace

Repository workspace for the OTPAuth platform.

## Current folders

- `mobile` - Android app scaffold
- `backend` - .NET backend scaffold
- `admin` - React admin scaffold
- `infra` - infrastructure assets
- `config/mcp` - local MCP configuration examples
- `OTP` - project knowledge vault

## Notes

- `mobile` is the canonical Android project root
- `admin` is the canonical frontend/admin root
- `backend` is the canonical .NET workspace root

## Working commands

### Admin

```powershell
cd .\admin
npm install
npm run build
```

### Backend

```powershell
cd .\backend
dotnet restore .\OtpAuth.slnx
dotnet build .\OtpAuth.slnx
```

### Mobile

```powershell
cd .\mobile
$env:JAVA_HOME='C:\Program Files\Android\openjdk\jdk-21.0.8'
$env:Path="$env:JAVA_HOME\bin;$env:Path"
.\gradlew.bat :app:dependencies --configuration debugRuntimeClasspath
```

If `JAVA_HOME` is already configured in your shell or IDE, you only need the Gradle command.
