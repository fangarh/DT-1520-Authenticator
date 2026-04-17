Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts\Installer.Common.ps1')

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param(
        $Actual,
        $Expected,
        [string]$Message
    )

    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Find-PlanStep {
    param(
        [Parameter(Mandatory)]$Plan,
        [Parameter(Mandatory)][string]$Name
    )

    return $Plan | Where-Object Name -eq $Name | Select-Object -First 1
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "otpauth-installer-tests-$([guid]::NewGuid().ToString('N'))"
$runtimeRoot = Join-Path $tempRoot 'runtime'
New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null

$tlsCertPath = Join-Path $runtimeRoot 'tls.crt'
$tlsKeyPath = Join-Path $runtimeRoot 'tls.key'
Set-Content -LiteralPath $tlsCertPath -Value 'cert' -Encoding Ascii
Set-Content -LiteralPath $tlsKeyPath -Value 'key' -Encoding Ascii

$envFilePath = Join-Path $runtimeRoot 'runtime.env'
@"
ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=otpauth;Username=otpauth;Password=secret;SSL Mode=Disable
BootstrapOAuth__CurrentSigningKeyId=current
BootstrapOAuth__CurrentSigningKey=signing-key
TotpProtection__CurrentKeyVersion=1
TotpProtection__CurrentKey=totp-key
OTPAUTH_TLS_CERT_PATH=$tlsCertPath
OTPAUTH_TLS_KEY_PATH=$tlsKeyPath
OTPAUTH_ADMIN_HTTPS_PORT=9443
"@ | Set-Content -LiteralPath $envFilePath -Encoding Ascii

$parsedEnv = Read-InstallerEnvironmentFile -Path $envFilePath
Assert-Equal $parsedEnv['BootstrapOAuth__CurrentSigningKeyId'] 'current' 'Env parser should keep key values.'

$manifest = New-InstallerManifest `
    -RepositoryRoot $repoRoot `
    -InfraRoot (Join-Path $repoRoot 'infra') `
    -Mode Install `
    -EnvFilePath $envFilePath `
    -ComposeFilePath (Join-Path $repoRoot 'infra\docker-compose.yml') `
    -BootstrapAdminUsername 'operator' `
    -BootstrapAdminPermissions @('enrollments.read', 'enrollments.write') `
    -ReportJsonPath (Join-Path $runtimeRoot 'report.json') `
    -SkipImageBuild:$false `
    -SkipBootstrap:$false `
    -SkipBootstrapAdmin:$false `
    -SkipPortAvailabilityCheck:$false `
    -SkipPortAvailabilityCheckWasSpecified:$false `
    -PreflightOnly:$false `
    -DryRun:$false

Assert-Equal $manifest.SchemaVersion 'otpauth.installer.manifest.v1' 'Installer manifest should expose a stable schema version.'
Assert-Equal $manifest.BootstrapAdmin.Username 'operator' 'Installer manifest should keep bootstrap admin username.'

$configuration = Get-InstallerConfiguration `
    -RepositoryRoot $repoRoot `
    -ComposeFilePath (Join-Path $repoRoot 'infra\docker-compose.yml') `
    -EnvFilePath $envFilePath `
    -BootstrapAdminUsername 'operator' `
    -BootstrapAdminPermissions @('enrollments.read', 'enrollments.write')

Assert-Equal $configuration.AdminHttpsPort 9443 'Admin HTTPS port should be read from env file.'
Assert-True (Test-InstallerWritableDirectory -DirectoryPath $runtimeRoot) 'Runtime root should be writable.'
Assert-True (Test-InstallerPortAvailable -Port 0) 'Port probe should work for an ephemeral port.'

$installProfile = Get-InstallerOperationProfile -Mode Install
Assert-True $installProfile.RequireFreeAdminPort 'Install mode should require a free admin port.'

$updateProfile = Get-InstallerOperationProfile -Mode Update
Assert-True (-not $updateProfile.IncludeBootstrapAdmin) 'Update mode should skip bootstrap admin by default.'

$recoverProfile = Get-InstallerOperationProfile -Mode Recover
Assert-True (-not $recoverProfile.IncludeImageBuild) 'Recover mode should skip image build by default.'
Assert-True (-not $recoverProfile.IncludeBootstrap) 'Recover mode should skip bootstrap by default.'

$validationIssues = @(Get-InstallerConfigurationValidationIssues `
    -RepositoryRoot $repoRoot `
    -ComposeFilePath (Join-Path $repoRoot 'infra\docker-compose.yml') `
    -EnvFilePath $envFilePath `
    -BootstrapAdminUsername 'operator' `
    -BootstrapAdminPermissions @('enrollments.read'))

Assert-Equal $validationIssues.Count 0 'Valid installer configuration should not return validation issues.'

$plan = New-InstallerExecutionPlan -Configuration $configuration -Mode Install
Assert-Equal $plan.Count 8 'Default install plan should contain eight steps.'
Assert-Equal $plan[0].Name 'Build images' 'First install step should build images.'
Assert-Equal $plan[0].Id 'build_images' 'Plan step should expose a stable machine-readable id.'
Assert-True (((Find-PlanStep -Plan $plan -Name 'Ensure database').Arguments -join ' ') -match 'ensure-database') 'Execution plan should include ensure-database.'
Assert-True (((Find-PlanStep -Plan $plan -Name 'Apply migrations').Arguments -join ' ') -match 'migrate') 'Execution plan should include migrate.'
$runtimeStep = Find-PlanStep -Plan $plan -Name 'Start runtime services'
Assert-True (($runtimeStep.Arguments -join ' ') -match 'up -d --wait api admin') 'Runtime start step should wait for api and admin.'
Assert-True ($runtimeStep.EnvironmentOverrides.Count -eq 0) 'Non-sensitive runtime steps should not mutate process env.'
$workerStep = Find-PlanStep -Plan $plan -Name 'Start worker'
Assert-True (($workerStep.Arguments -join ' ') -match 'up -d --wait worker') 'Worker start step should wait for worker health.'
Assert-Equal $plan[-1].Name 'Show runtime status' 'Last install step should emit runtime status.'
Assert-True ($plan[-1].CaptureOutput) 'Runtime status step should capture sanitized output for reporting.'

$updatePlan = New-InstallerExecutionPlan -Configuration $configuration -Mode Update
Assert-Equal $updatePlan.Count 7 'Default update plan should contain seven steps.'
Assert-True ($null -eq (Find-PlanStep -Plan $updatePlan -Name 'Upsert bootstrap admin user')) 'Update plan should not upsert bootstrap admin by default.'
Assert-True ($null -ne (Find-PlanStep -Plan $updatePlan -Name 'Apply migrations')) 'Update plan should keep migrations.'

$recoverPlan = New-InstallerExecutionPlan -Configuration $configuration -Mode Recover
Assert-Equal $recoverPlan.Count 4 'Default recover plan should contain four steps.'
Assert-True ($null -eq (Find-PlanStep -Plan $recoverPlan -Name 'Build images')) 'Recover plan should skip image build.'
Assert-True ($null -eq (Find-PlanStep -Plan $recoverPlan -Name 'Ensure database')) 'Recover plan should skip bootstrap.'
Assert-True ($null -eq (Find-PlanStep -Plan $recoverPlan -Name 'Upsert bootstrap admin user')) 'Recover plan should skip bootstrap admin.'

$bootstrapFreePlan = New-InstallerExecutionPlan -Configuration $configuration -Mode Install -SkipImageBuild -SkipBootstrap -SkipBootstrapAdmin
Assert-Equal $bootstrapFreePlan.Count 4 'Plan without build/bootstrap should contain infrastructure, runtime start, worker start and status steps only.'

$fakeCommandRunner = {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [hashtable]$EnvironmentOverrides,
        [bool]$CaptureOutput
    )

    return [pscustomobject]@{
        ExitCode = 0
        OutputLines = if ($CaptureOutput) { @('NAME STATUS', 'api running') } else { @() }
    }
}

$stepResults = @(Invoke-InstallerExecutionPlan -ExecutionPlan $plan -CommandRunner $fakeCommandRunner)
Assert-Equal $stepResults.Count 8 'Execution plan invocation should return one result per executed step.'
Assert-True (@($stepResults | Where-Object Status -eq 'failed').Count -eq 0) 'Happy-path execution results should not contain failed steps.'
Assert-Equal $stepResults[-1].StepId 'show_runtime_status' 'Status report step result should keep the machine-readable step id.'

$report = New-InstallerExecutionReport `
    -Manifest $manifest `
    -OperationProfile $installProfile `
    -Configuration $configuration `
    -StepResults $stepResults

Assert-Equal $report.Outcome 'succeeded' 'Successful step results should yield a succeeded installer report.'
Assert-Equal $report.RuntimeStatus.Lines[0] 'NAME STATUS' 'Installer report should surface sanitized runtime status lines.'

$composeStatusJson = @'
[
  {
    "Service": "api",
    "State": "running",
    "Health": "healthy",
    "ExitCode": 0,
    "Publishers": []
  },
  {
    "Service": "worker",
    "State": "running",
    "Health": "healthy",
    "ExitCode": 0,
    "Publishers": []
  }
]
'@

$workerHeartbeatJson = @'
{
  "ServiceName": "OtpAuth.Worker",
  "StartedAtUtc": "2026-04-15T12:00:00Z",
  "LastHeartbeatUtc": "2026-04-15T12:05:00Z",
  "LastExecutionStartedUtc": "2026-04-15T12:04:30Z",
  "LastExecutionCompletedUtc": "2026-04-15T12:04:31Z",
  "ExecutionOutcome": "healthy",
  "ConsecutiveFailureCount": 0,
  "DependencyStatuses": [
    {
      "Name": "postgres",
      "Status": "healthy",
      "CheckedAtUtc": "2026-04-15T12:05:00Z",
      "FailureKind": null
    }
  ],
  "JobStatuses": [
    {
      "Name": "security_data_cleanup",
      "Status": "healthy",
      "IntervalSeconds": 300,
      "IsDue": false,
      "LastStartedAtUtc": "2026-04-15T12:04:30Z",
      "LastCompletedAtUtc": "2026-04-15T12:04:31Z",
      "LastSuccessfulCompletedAtUtc": "2026-04-15T12:04:31Z",
      "SuccessfulRunCount": 1,
      "FailedRunCount": 0,
      "ConsecutiveFailureCount": 0,
      "LastSummary": "cleanup_completed",
      "FailureKind": null,
      "LastMetrics": [
        {
          "Name": "deletedTotal",
          "Value": 7
        }
      ]
    }
  ]
}
'@

$parsedComposeStatus = ConvertFrom-InstallerComposeStatusJson -OutputLines @($composeStatusJson)
Assert-Equal $parsedComposeStatus.Count 2 'Structured compose status parsing should return all services.'
Assert-Equal $parsedComposeStatus[1].Service 'worker' 'Structured compose status should keep service names.'

$parsedWorkerHeartbeat = ConvertFrom-InstallerWorkerHeartbeatJson -OutputLines @($workerHeartbeatJson)
Assert-Equal $parsedWorkerHeartbeat.ExecutionOutcome 'healthy' 'Worker heartbeat parsing should keep execution outcome.'
Assert-Equal $parsedWorkerHeartbeat.JobStatuses[0].LastMetrics[0].Value 7 'Worker heartbeat parsing should keep sanitized metrics.'

$diagnosticCommandRunner = {
    param(
        $FilePath,
        $Arguments,
        $EnvironmentOverrides,
        $CaptureOutput
    )

    $commandLine = $Arguments -join ' '
    if ($commandLine -match 'ps --format json') {
        return [pscustomobject]@{
            ExitCode = 0
            OutputLines = @($composeStatusJson)
        }
    }

    if ($commandLine -match 'exec -T worker /bin/sh -c cat /tmp/otpauth-worker/heartbeat\.json') {
        return [pscustomobject]@{
            ExitCode = 0
            OutputLines = @($workerHeartbeatJson)
        }
    }

    throw "Unexpected diagnostics command: $commandLine"
}

$runtimeDiagnostics = Get-InstallerRuntimeDiagnostics -Configuration $configuration -CommandRunner $diagnosticCommandRunner
Assert-Equal $runtimeDiagnostics.Services.Count 2 'Runtime diagnostics should expose structured runtime services.'
Assert-Equal $runtimeDiagnostics.Worker.ServiceName 'OtpAuth.Worker' 'Runtime diagnostics should expose worker heartbeat snapshot.'
Assert-Equal $runtimeDiagnostics.TroubleshootingHints.Count 0 'Healthy runtime diagnostics should not create troubleshooting hints.'

$degradedWorkerHeartbeat = [pscustomobject]@{
    ServiceName = 'OtpAuth.Worker'
    ExecutionOutcome = 'degraded'
    ConsecutiveFailureCount = 2
    DependencyStatuses = @(
        [pscustomobject]@{
            Name = 'postgres'
            Status = 'degraded'
            CheckedAtUtc = '2026-04-15T12:05:00Z'
            FailureKind = 'connection_failed'
        }
    )
    JobStatuses = @(
        [pscustomobject]@{
            Name = 'security_data_cleanup'
            Status = 'blocked'
            FailureKind = 'blocked_by_dependency'
        }
    )
}

$degradedDiagnostics = [pscustomobject]@{
    Services = @(
        [pscustomobject]@{
            Service = 'worker'
            State = 'running'
            Health = 'healthy'
            ExitCode = 0
            Publishers = @()
        }
    )
    Worker = $degradedWorkerHeartbeat
    DiagnosticIssues = @()
    TroubleshootingHints = @(Get-InstallerTroubleshootingHints -RuntimeServices @([pscustomobject]@{
                Service = 'worker'
                State = 'running'
                Health = 'healthy'
                ExitCode = 0
                Publishers = @()
            }) -WorkerHeartbeat $degradedWorkerHeartbeat)
}

$degradedReport = New-InstallerExecutionReport `
    -Manifest $manifest `
    -OperationProfile $installProfile `
    -Configuration $configuration `
    -StepResults $stepResults `
    -RuntimeDiagnostics $degradedDiagnostics

Assert-Equal $degradedReport.Outcome 'degraded' 'Troubleshooting hints should downgrade the installer report outcome.'
Assert-True ($degradedReport.TroubleshootingHints.Count -ge 2) 'Degraded diagnostics should surface troubleshooting hints in the report.'

$reportJsonPath = Join-Path $runtimeRoot 'installer-report.json'
Write-InstallerExecutionReportJson -Report $report -Path $reportJsonPath
$reportJson = Get-Content -Raw $reportJsonPath
Assert-True ($reportJson -notmatch 'signing-key') 'Installer report JSON must not contain signing key material.'
Assert-True ($reportJson -notmatch 'totp-key') 'Installer report JSON must not contain TOTP key material.'
Assert-True ($reportJson -notmatch 'Password=secret') 'Installer report JSON must not contain connection string secret material.'

$repoLocalEnvPath = Join-Path $repoRoot 'infra\env\runtime.env.example'
$threwForRepoLocalEnv = $false
try {
    Get-InstallerConfiguration `
        -RepositoryRoot $repoRoot `
        -ComposeFilePath (Join-Path $repoRoot 'infra\docker-compose.yml') `
        -EnvFilePath $repoLocalEnvPath `
        -BootstrapAdminUsername 'operator' `
        -BootstrapAdminPermissions @('enrollments.read') | Out-Null
}
catch {
    $threwForRepoLocalEnv = $true
}

Assert-True $threwForRepoLocalEnv 'Installer config should reject env files stored inside the repository.'

$missingKeyEnvPath = Join-Path $runtimeRoot 'runtime-missing.env'
@"
ConnectionStrings__Postgres=Host=postgres
BootstrapOAuth__CurrentSigningKeyId=current
TotpProtection__CurrentKeyVersion=1
TotpProtection__CurrentKey=totp-key
OTPAUTH_TLS_CERT_PATH=$tlsCertPath
OTPAUTH_TLS_KEY_PATH=$tlsKeyPath
"@ | Set-Content -LiteralPath $missingKeyEnvPath -Encoding Ascii

$missingKeyIssues = @(Get-InstallerConfigurationValidationIssues `
    -RepositoryRoot $repoRoot `
    -ComposeFilePath (Join-Path $repoRoot 'infra\docker-compose.yml') `
    -EnvFilePath $missingKeyEnvPath `
    -BootstrapAdminUsername 'operator' `
    -BootstrapAdminPermissions @('enrollments.read'))

Assert-True (@($missingKeyIssues | Where-Object Code -eq 'env_key_missing' | Where-Object Field -eq 'BootstrapOAuth__CurrentSigningKey').Count -eq 1) 'Missing required env keys should be returned as structured validation issues.'

Remove-Item -LiteralPath $tempRoot -Recurse -Force
Write-Host 'Installer helper tests passed.'
