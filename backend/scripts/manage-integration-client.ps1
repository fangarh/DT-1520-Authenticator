param(
    [Parameter(Mandatory = $true)]
    [string]$ClientId,
    [switch]$RotateSecret,
    [switch]$Deactivate,
    [switch]$Activate,
    [string]$NewClientSecret
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$requestedOperations = @($RotateSecret, $Deactivate, $Activate) | Where-Object { $_ }
if ($requestedOperations.Count -ne 1) {
    throw "Specify exactly one operation: -RotateSecret, -Deactivate, or -Activate."
}

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

    if ($RotateSecret) {
        if (-not [string]::IsNullOrWhiteSpace($NewClientSecret)) {
            $env:OTPAUTH_NEW_CLIENT_SECRET = $NewClientSecret
        }

        dotnet run --no-build --project $migrationsProjectPath -- rotate-integration-client-secret $ClientId
    }
    elseif ($Deactivate) {
        dotnet run --no-build --project $migrationsProjectPath -- deactivate-integration-client $ClientId
    }
    else {
        dotnet run --no-build --project $migrationsProjectPath -- activate-integration-client $ClientId
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Integration client management command failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($RotateSecret -and -not [string]::IsNullOrWhiteSpace($NewClientSecret)) {
        Remove-Item Env:\OTPAUTH_NEW_CLIENT_SECRET -ErrorAction SilentlyContinue
    }

    Pop-Location
}
