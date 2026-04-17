Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-InstallerValidationIssue {
    param(
        [Parameter(Mandatory)][string]$Code,
        [Parameter(Mandatory)][string]$Field,
        [Parameter(Mandatory)][string]$Message
    )

    return [pscustomobject]@{
        Code = $Code
        Field = $Field
        Message = $Message
    }
}

function New-InstallerDiagnosticIssue {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Code,
        [Parameter(Mandatory)][string]$Message
    )

    return [pscustomobject]@{
        Source = $Source
        Code = $Code
        Message = $Message
    }
}

function New-InstallerTroubleshootingHint {
    param(
        [Parameter(Mandatory)][string]$Code,
        [Parameter(Mandatory)][ValidateSet('info', 'warning', 'error')][string]$Severity,
        [string]$Service,
        [string]$Component,
        [string]$FailureKind,
        [Parameter(Mandatory)][string]$Message,
        [Parameter(Mandatory)][string]$RecommendedAction
    )

    return [pscustomobject]@{
        Code = $Code
        Severity = $Severity
        Service = $Service
        Component = $Component
        FailureKind = $FailureKind
        Message = $Message
        RecommendedAction = $RecommendedAction
    }
}

function Format-InstallerValidationIssue {
    param([Parameter(Mandatory)]$Issue)

    return "[$($Issue.Code)] $($Issue.Field): $($Issue.Message)"
}

function New-InstallerManifest {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$InfraRoot,
        [Parameter(Mandatory)][ValidateSet('Install', 'Update', 'Recover')][string]$Mode,
        [Parameter(Mandatory)][string]$EnvFilePath,
        [Parameter(Mandatory)][string]$ComposeFilePath,
        [Parameter(Mandatory)][string]$BootstrapAdminUsername,
        [Parameter(Mandatory)][string[]]$BootstrapAdminPermissions,
        [string]$ReportJsonPath,
        [bool]$SkipImageBuild,
        [bool]$SkipBootstrap,
        [bool]$SkipBootstrapAdmin,
        [bool]$SkipPortAvailabilityCheck,
        [bool]$SkipPortAvailabilityCheckWasSpecified,
        [bool]$PreflightOnly,
        [bool]$DryRun
    )

    return [pscustomobject]@{
        SchemaVersion = 'otpauth.installer.manifest.v1'
        CreatedUtc = [DateTime]::UtcNow
        Mode = $Mode
        Paths = [pscustomobject]@{
            RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
            InfraRoot = [System.IO.Path]::GetFullPath($InfraRoot)
            EnvFilePath = [System.IO.Path]::GetFullPath($EnvFilePath)
            ComposeFilePath = [System.IO.Path]::GetFullPath($ComposeFilePath)
            ReportJsonPath = if ([string]::IsNullOrWhiteSpace($ReportJsonPath)) {
                $null
            }
            else {
                [System.IO.Path]::GetFullPath($ReportJsonPath)
            }
        }
        BootstrapAdmin = [pscustomobject]@{
            Username = $BootstrapAdminUsername
            Permissions = @($BootstrapAdminPermissions)
        }
        Options = [pscustomobject]@{
            SkipImageBuild = $SkipImageBuild
            SkipBootstrap = $SkipBootstrap
            SkipBootstrapAdmin = $SkipBootstrapAdmin
            SkipPortAvailabilityCheck = $SkipPortAvailabilityCheck
            SkipPortAvailabilityCheckWasSpecified = $SkipPortAvailabilityCheckWasSpecified
            PreflightOnly = $PreflightOnly
            DryRun = $DryRun
        }
    }
}

function New-InstallerPlanStep {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][hashtable]$EnvironmentOverrides,
        [bool]$CaptureOutput
    )

    return [pscustomobject]@{
        Id = $Id
        Name = $Name
        FilePath = $FilePath
        Arguments = @($Arguments)
        EnvironmentOverrides = $EnvironmentOverrides
        EnvironmentOverrideKeys = @($EnvironmentOverrides.Keys | Sort-Object)
        CommandPreview = "$FilePath $($Arguments -join ' ')"
        CaptureOutput = $CaptureOutput
    }
}

function New-InstallerStepResult {
    param(
        [Parameter(Mandatory)]$Step,
        [Parameter(Mandatory)][ValidateSet('succeeded', 'failed')][string]$Status,
        [Parameter(Mandatory)][DateTime]$StartedUtc,
        [Parameter(Mandatory)][DateTime]$CompletedUtc,
        [Parameter(Mandatory)][string]$Message,
        [int]$ExitCode = 0,
        [string[]]$OutputLines = @()
    )

    return [pscustomobject]@{
        StepId = $Step.Id
        Name = $Step.Name
        Status = $Status
        StartedUtc = $StartedUtc
        CompletedUtc = $CompletedUtc
        DurationMs = [int][Math]::Max(0, ($CompletedUtc - $StartedUtc).TotalMilliseconds)
        Message = $Message
        ExitCode = $ExitCode
        CommandPreview = $Step.CommandPreview
        EnvironmentOverrideKeys = @($Step.EnvironmentOverrideKeys)
        OutputLines = @($OutputLines)
    }
}

function Get-InstallerConfigurationSummary {
    param([Parameter(Mandatory)]$Configuration)

    return [pscustomobject]@{
        ComposeFilePath = $Configuration.ComposeFilePath
        EnvFilePath = $Configuration.EnvFilePath
        EnvDirectoryPath = $Configuration.EnvDirectoryPath
        AdminHttpsPort = $Configuration.AdminHttpsPort
        BootstrapAdminUsername = $Configuration.BootstrapAdminUsername
        BootstrapAdminPermissions = @($Configuration.BootstrapAdminPermissions)
    }
}

function New-InstallerExecutionReport {
    param(
        [Parameter(Mandatory)]$Manifest,
        $OperationProfile,
        $Configuration,
        [object[]]$ValidationIssues = @(),
        [object[]]$StepResults = @(),
        $RuntimeDiagnostics = $null,
        [string]$FailureMessage
    )

    $failedStepResults = @($StepResults | Where-Object Status -eq 'failed')
    $diagnosticIssues = if ($null -eq $RuntimeDiagnostics) { @() } else { @($RuntimeDiagnostics.DiagnosticIssues) }
    $troubleshootingHints = if ($null -eq $RuntimeDiagnostics) { @() } else { @($RuntimeDiagnostics.TroubleshootingHints) }
    $hasDiagnosticWarnings = @($diagnosticIssues).Count -gt 0 -or @($troubleshootingHints | Where-Object Severity -in @('warning', 'error')).Count -gt 0
    $outcome = if ($ValidationIssues.Count -gt 0 -or $failedStepResults.Count -gt 0 -or -not [string]::IsNullOrWhiteSpace($FailureMessage)) {
        'failed'
    }
    elseif ($hasDiagnosticWarnings) {
        'degraded'
    }
    else {
        'succeeded'
    }

    $runtimeStatusStep = $StepResults | Where-Object StepId -eq 'show_runtime_status' | Select-Object -Last 1
    $stepStartedUtcValues = @($StepResults | ForEach-Object StartedUtc)
    $stepCompletedUtcValues = @($StepResults | ForEach-Object CompletedUtc)
    $startedUtc = if ($stepStartedUtcValues.Count -gt 0) {
        ($stepStartedUtcValues | Sort-Object | Select-Object -First 1)
    }
    else {
        $null
    }
    $completedUtc = if ($stepCompletedUtcValues.Count -gt 0) {
        ($stepCompletedUtcValues | Sort-Object | Select-Object -Last 1)
    }
    else {
        $null
    }

    return [pscustomobject]@{
        SchemaVersion = 'otpauth.installer.report.v1'
        GeneratedUtc = [DateTime]::UtcNow
        Outcome = $outcome
        Mode = $Manifest.Mode
        PreflightOnly = $Manifest.Options.PreflightOnly
        DryRun = $Manifest.Options.DryRun
        OperationProfile = if ($null -eq $OperationProfile) {
            $null
        }
        else {
            [pscustomobject]@{
                Mode = $OperationProfile.Mode
                DisplayName = $OperationProfile.DisplayName
            }
        }
        Manifest = [pscustomobject]@{
            SchemaVersion = $Manifest.SchemaVersion
            Paths = [pscustomobject]@{
                ComposeFilePath = $Manifest.Paths.ComposeFilePath
                EnvFilePath = $Manifest.Paths.EnvFilePath
                ReportJsonPath = $Manifest.Paths.ReportJsonPath
            }
            BootstrapAdmin = [pscustomobject]@{
                Username = $Manifest.BootstrapAdmin.Username
                Permissions = @($Manifest.BootstrapAdmin.Permissions)
            }
            Options = $Manifest.Options
        }
        Configuration = if ($null -eq $Configuration) {
            $null
        }
        else {
            Get-InstallerConfigurationSummary -Configuration $Configuration
        }
        ValidationIssues = @($ValidationIssues)
        DiagnosticIssues = @($diagnosticIssues)
        StepResults = @($StepResults)
        RuntimeStatus = [pscustomobject]@{
            Lines = if ($null -eq $runtimeStatusStep) {
                @()
            }
            else {
                @($runtimeStatusStep.OutputLines)
            }
            Services = if ($null -eq $RuntimeDiagnostics) {
                @()
            }
            else {
                @($RuntimeDiagnostics.Services)
            }
        }
        WorkerDiagnostics = if ($null -eq $RuntimeDiagnostics) {
            $null
        }
        else {
            $RuntimeDiagnostics.Worker
        }
        TroubleshootingHints = @($troubleshootingHints)
        Summary = [pscustomobject]@{
            TotalSteps = $StepResults.Count
            SucceededSteps = @($StepResults | Where-Object Status -eq 'succeeded').Count
            FailedSteps = $failedStepResults.Count
            DiagnosticIssueCount = @($diagnosticIssues).Count
            TroubleshootingHintCount = @($troubleshootingHints).Count
            StartedUtc = $startedUtc
            CompletedUtc = $completedUtc
            DurationMs = if ($null -eq $startedUtc -or $null -eq $completedUtc) {
                0
            }
            else {
                [int][Math]::Max(0, ($completedUtc - $startedUtc).TotalMilliseconds)
            }
        }
        FailureMessage = $FailureMessage
    }
}

function Write-InstallerExecutionReportJson {
    param(
        [Parameter(Mandatory)]$Report,
        [Parameter(Mandatory)][string]$Path
    )

    $reportDirectoryPath = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($reportDirectoryPath)) {
        New-Item -ItemType Directory -Path $reportDirectoryPath -Force | Out-Null
    }

    $reportJson = $Report | ConvertTo-Json -Depth 8
    Set-Content -LiteralPath $Path -Value $reportJson -Encoding UTF8
}
