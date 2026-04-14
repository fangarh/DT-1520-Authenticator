param(
    [switch]$ReEncryptTotpSecrets,
    [switch]$CleanupSecurityData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendRoot = Split-Path -Parent $scriptRoot
$migrationsProjectPath = Join-Path $backendRoot 'OtpAuth.Migrations\OtpAuth.Migrations.csproj'

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Postgres)) {
    throw "Environment variable 'ConnectionStrings__Postgres' is required."
}

if (-not $ReEncryptTotpSecrets -and -not $CleanupSecurityData) {
    throw "Specify at least one switch: -ReEncryptTotpSecrets or -CleanupSecurityData."
}

Push-Location $backendRoot
try {
    Write-Host 'Building PostgreSQL migration runner sequentially...'
    dotnet build $migrationsProjectPath -p:BuildInParallel=false -p:RestoreBuildInParallel=false -maxcpucount:1

    if ($LASTEXITCODE -ne 0) {
        throw "Migration runner build failed with exit code $LASTEXITCODE."
    }

    if ($ReEncryptTotpSecrets) {
        if ([string]::IsNullOrWhiteSpace($env:TotpProtection__CurrentKey)) {
            throw "Environment variable 'TotpProtection__CurrentKey' is required when -ReEncryptTotpSecrets is used."
        }

        Write-Host 'Re-encrypting stored TOTP secrets...'
        dotnet run --no-build --project $migrationsProjectPath -- reencrypt-totp-secrets

        if ($LASTEXITCODE -ne 0) {
            throw "TOTP re-encryption command failed with exit code $LASTEXITCODE."
        }
    }

    if ($CleanupSecurityData) {
        Write-Host 'Cleaning up expired security data...'
        dotnet run --no-build --project $migrationsProjectPath -- cleanup-security-data

        if ($LASTEXITCODE -ne 0) {
            throw "Security cleanup command failed with exit code $LASTEXITCODE."
        }
    }
}
finally {
    Pop-Location
}
