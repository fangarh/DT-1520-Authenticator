param(
    [switch]$ListRecent,
    [ValidateRange(1, 100)]
    [int]$Limit = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Postgres)) {
    throw "Environment variable 'ConnectionStrings__Postgres' is required."
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendRoot = Split-Path -Parent $scriptRoot
$migrationsProjectPath = Join-Path $backendRoot 'OtpAuth.Migrations\OtpAuth.Migrations.csproj'

Push-Location $backendRoot
try {
    Write-Host 'Building PostgreSQL migration runner sequentially...'
    dotnet build $migrationsProjectPath -p:BuildInParallel=false -p:RestoreBuildInParallel=false -maxcpucount:1

    if ($LASTEXITCODE -ne 0) {
        throw "Migration runner build failed with exit code $LASTEXITCODE."
    }

    if ($ListRecent) {
        dotnet run --no-build --project $migrationsProjectPath -- list-totp-protection-key-lifecycle-audit-events $Limit
    }
    else {
        dotnet run --no-build --project $migrationsProjectPath -- audit-totp-protection-key-lifecycle
    }

    if ($LASTEXITCODE -ne 0) {
        throw "TOTP protection key lifecycle audit command failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
