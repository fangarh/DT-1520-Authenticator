Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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

    dotnet run --no-build --project $migrationsProjectPath -- inspect-signing-key-lifecycle

    if ($LASTEXITCODE -ne 0) {
        throw "Signing key lifecycle inspection failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
