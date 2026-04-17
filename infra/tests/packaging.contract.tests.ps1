Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-FileExists {
    param([string]$Path)

    Assert-True (Test-Path -LiteralPath $Path) "Expected file '$Path' to exist."
}

function Assert-FileContains {
    param(
        [string]$Path,
        [string]$Pattern
    )

    $content = Get-Content -Raw -LiteralPath $Path
    Assert-True ($content -match $Pattern) "Expected file '$Path' to match pattern '$Pattern'."
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$requiredFiles = @(
    ".dockerignore",
    "infra/docker-compose.yml",
    "infra/docker/api.Dockerfile",
    "infra/docker/worker.Dockerfile",
    "infra/docker/bootstrap.Dockerfile",
    "infra/docker/admin.Dockerfile",
    "infra/nginx/admin.conf",
    "infra/env/runtime.env.example",
    "infra/scripts/Installer.Contract.ps1",
    "infra/scripts/Installer.Common.ps1",
    "infra/scripts/Installer.Diagnostics.ps1",
    "infra/scripts/install.ps1",
    "infra/tests/installer.common.tests.ps1"
)

foreach ($relativePath in $requiredFiles) {
    Assert-FileExists (Join-Path $repoRoot $relativePath)
}

$composePath = Join-Path $repoRoot "infra/docker-compose.yml"
Assert-FileContains $composePath "(?ms)services:\s+postgres:"
Assert-FileContains $composePath "(?ms)services:.*\s+redis:"
Assert-FileContains $composePath "(?ms)services:.*\s+bootstrap:"
Assert-FileContains $composePath "(?ms)services:.*\s+api:"
Assert-FileContains $composePath "(?ms)services:.*\s+worker:"
Assert-FileContains $composePath "(?ms)services:.*\s+admin:"
Assert-FileContains $composePath "profiles:\s+- bootstrap"
Assert-FileContains $composePath "condition:\s+service_healthy"
Assert-FileContains $composePath "OTPAUTH_TLS_CERT_PATH"
Assert-FileContains $composePath "BootstrapOAuth__CurrentSigningKey"
Assert-FileContains $composePath "TotpProtection__CurrentKey"
Assert-FileContains $composePath "WorkerDiagnostics__HeartbeatFilePath"
Assert-FileContains $composePath "find /tmp/otpauth-worker/heartbeat\.json -mmin -2"

$nginxConfigPath = Join-Path $repoRoot "infra/nginx/admin.conf"
Assert-FileContains $nginxConfigPath "listen 8443 ssl;"
Assert-FileContains $nginxConfigPath "location /api/"
Assert-FileContains $nginxConfigPath "location /oauth2/"
Assert-FileContains $nginxConfigPath "Content-Security-Policy"

$adminDockerfilePath = Join-Path $repoRoot "infra/docker/admin.Dockerfile"
Assert-FileContains $adminDockerfilePath "nginx-unprivileged"
Assert-FileContains $adminDockerfilePath "VITE_ADMIN_API_BASE_URL"

$apiDockerfilePath = Join-Path $repoRoot "infra/docker/api.Dockerfile"
Assert-FileContains $apiDockerfilePath "ASPNETCORE_URLS=http://\+:8080"
Assert-FileContains $apiDockerfilePath 'ENTRYPOINT \["dotnet", "OtpAuth\.Api\.dll"\]'

$workerDockerfilePath = Join-Path $repoRoot "infra/docker/worker.Dockerfile"
Assert-FileContains $workerDockerfilePath 'ENTRYPOINT \["dotnet", "OtpAuth\.Worker\.dll"\]'

$bootstrapDockerfilePath = Join-Path $repoRoot "infra/docker/bootstrap.Dockerfile"
Assert-FileContains $bootstrapDockerfilePath 'ENTRYPOINT \["dotnet", "OtpAuth\.Migrations\.dll"\]'

$installScriptPath = Join-Path $repoRoot "infra/scripts/install.ps1"
Assert-FileContains $installScriptPath "PreflightOnly"
Assert-FileContains $installScriptPath "SkipImageBuild"
Assert-FileContains $installScriptPath "ReportJsonPath"

$installerCommonPath = Join-Path $repoRoot "infra/scripts/Installer.Common.ps1"
Assert-FileContains $installerCommonPath "Get-InstallerConfiguration"
Assert-FileContains $installerCommonPath "New-InstallerExecutionPlan"
Assert-FileContains $installerCommonPath "Get-InstallerPreflightValidationIssues"
Assert-FileContains $installerCommonPath "Invoke-InstallerExecutionPlan"
Assert-FileContains $installerCommonPath "Installer.Diagnostics.ps1"

$installerContractPath = Join-Path $repoRoot "infra/scripts/Installer.Contract.ps1"
Assert-FileContains $installerContractPath "New-InstallerManifest"
Assert-FileContains $installerContractPath "Write-InstallerExecutionReportJson"

$installerDiagnosticsPath = Join-Path $repoRoot "infra/scripts/Installer.Diagnostics.ps1"
Assert-FileContains $installerDiagnosticsPath "Get-InstallerRuntimeDiagnostics"
Assert-FileContains $installerDiagnosticsPath "ConvertFrom-InstallerWorkerHeartbeatJson"

Write-Host "Packaging contract checks passed."
