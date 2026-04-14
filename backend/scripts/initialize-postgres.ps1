param(
    [switch]$SeedBootstrapClients,
    [switch]$SeedBootstrapTotpEnrollment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendRoot = Split-Path -Parent $scriptRoot
$migrationsProjectPath = Join-Path $backendRoot 'OtpAuth.Migrations\OtpAuth.Migrations.csproj'

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Postgres)) {
    throw "Environment variable 'ConnectionStrings__Postgres' is required."
}

Push-Location $backendRoot
try {
    Write-Host 'Building PostgreSQL migration runner sequentially...'
    dotnet build $migrationsProjectPath -p:BuildInParallel=false -p:RestoreBuildInParallel=false -maxcpucount:1

    if ($LASTEXITCODE -ne 0) {
        throw "Migration runner build failed with exit code $LASTEXITCODE."
    }

    Write-Host 'Ensuring PostgreSQL database exists...'
    dotnet run --no-build --project $migrationsProjectPath -- ensure-database

    if ($LASTEXITCODE -ne 0) {
        throw "Ensure-database command failed with exit code $LASTEXITCODE."
    }

    Write-Host 'Applying PostgreSQL migrations...'
    dotnet run --no-build --project $migrationsProjectPath -- migrate

    if ($LASTEXITCODE -ne 0) {
        throw "Migration command failed with exit code $LASTEXITCODE."
    }

    if ($SeedBootstrapClients) {
        if ([string]::IsNullOrWhiteSpace($env:OTPAUTH_BOOTSTRAP_CLIENT_SECRET)) {
            throw "Environment variable 'OTPAUTH_BOOTSTRAP_CLIENT_SECRET' is required when -SeedBootstrapClients is used."
        }

        Write-Host 'Seeding bootstrap integration clients...'
        dotnet run --no-build --project $migrationsProjectPath -- seed-bootstrap-clients

        if ($LASTEXITCODE -ne 0) {
            throw "Bootstrap client seed command failed with exit code $LASTEXITCODE."
        }
    }

    if ($SeedBootstrapTotpEnrollment) {
        if ([string]::IsNullOrWhiteSpace($env:OTPAUTH_BOOTSTRAP_TOTP_EXTERNAL_USER_ID)) {
            throw "Environment variable 'OTPAUTH_BOOTSTRAP_TOTP_EXTERNAL_USER_ID' is required when -SeedBootstrapTotpEnrollment is used."
        }

        if ([string]::IsNullOrWhiteSpace($env:OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64)) {
            throw "Environment variable 'OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64' is required when -SeedBootstrapTotpEnrollment is used."
        }

        if ([string]::IsNullOrWhiteSpace($env:TotpProtection__CurrentKey)) {
            throw "Environment variable 'TotpProtection__CurrentKey' is required when -SeedBootstrapTotpEnrollment is used."
        }

        Write-Host 'Seeding bootstrap TOTP enrollment...'
        dotnet run --no-build --project $migrationsProjectPath -- seed-bootstrap-totp-enrollment

        if ($LASTEXITCODE -ne 0) {
            throw "Bootstrap TOTP enrollment seed command failed with exit code $LASTEXITCODE."
        }
    }
}
finally {
    Pop-Location
}
