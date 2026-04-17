param(
    [Parameter(Mandatory)]
    [string]$EnvFilePath,
    [ValidateSet('Install', 'Update', 'Recover')]
    [string]$Mode = 'Install',
    [string]$ComposeFilePath,
    [string]$BootstrapAdminUsername = 'operator',
    [string[]]$BootstrapAdminPermissions = @('enrollments.read', 'enrollments.write'),
    [switch]$SkipImageBuild,
    [switch]$SkipBootstrap,
    [switch]$SkipBootstrapAdmin,
    [switch]$SkipPortAvailabilityCheck,
    [switch]$PreflightOnly,
    [switch]$DryRun,
    [string]$ReportJsonPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Installer.Common.ps1')

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$infraRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ComposeFilePath)) {
    $ComposeFilePath = Join-Path $infraRoot 'docker-compose.yml'
}

$manifest = New-InstallerManifest `
    -RepositoryRoot $repositoryRoot `
    -InfraRoot $infraRoot `
    -Mode $Mode `
    -EnvFilePath $EnvFilePath `
    -ComposeFilePath $ComposeFilePath `
    -BootstrapAdminUsername $BootstrapAdminUsername `
    -BootstrapAdminPermissions $BootstrapAdminPermissions `
    -ReportJsonPath $ReportJsonPath `
    -SkipImageBuild:$SkipImageBuild.IsPresent `
    -SkipBootstrap:$SkipBootstrap.IsPresent `
    -SkipBootstrapAdmin:$SkipBootstrapAdmin.IsPresent `
    -SkipPortAvailabilityCheck:$SkipPortAvailabilityCheck.IsPresent `
    -SkipPortAvailabilityCheckWasSpecified:$PSBoundParameters.ContainsKey('SkipPortAvailabilityCheck') `
    -PreflightOnly:$PreflightOnly.IsPresent `
    -DryRun:$DryRun.IsPresent

$configuration = $null
$operationProfile = $null
$report = $null
$runtimeDiagnostics = $null

$commandRunner = {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [hashtable]$EnvironmentOverrides,
        [bool]$CaptureOutput
    )

    if ($DryRun) {
        $commandLine = "$FilePath $($Arguments -join ' ')"
        Write-Host "[dry-run] $commandLine"
        return [pscustomobject]@{
            ExitCode = 0
            OutputLines = @("[dry-run] $commandLine")
        }
    }

    Invoke-InstallerCommand -FilePath $FilePath -Arguments $Arguments -EnvironmentOverrides $EnvironmentOverrides -CaptureOutput:$CaptureOutput
}

try {
    $configurationIssues = @(Get-InstallerConfigurationValidationIssues `
        -RepositoryRoot $repositoryRoot `
        -ComposeFilePath $ComposeFilePath `
        -EnvFilePath $EnvFilePath `
        -BootstrapAdminUsername $BootstrapAdminUsername `
        -BootstrapAdminPermissions $BootstrapAdminPermissions)

    if ($configurationIssues.Count -gt 0) {
        $report = New-InstallerExecutionReport -Manifest $manifest -ValidationIssues $configurationIssues
        throw ([string]::Join([Environment]::NewLine, @($configurationIssues | ForEach-Object { Format-InstallerValidationIssue -Issue $_ })))
    }

    $configuration = Get-InstallerConfiguration `
        -RepositoryRoot $repositoryRoot `
        -ComposeFilePath $ComposeFilePath `
        -EnvFilePath $EnvFilePath `
        -BootstrapAdminUsername $BootstrapAdminUsername `
        -BootstrapAdminPermissions $BootstrapAdminPermissions

    $operationProfile = Get-InstallerOperationProfile -Mode $Mode
    $shouldSkipPortAvailabilityCheck = if ($PSBoundParameters.ContainsKey('SkipPortAvailabilityCheck')) {
        $SkipPortAvailabilityCheck.IsPresent
    }
    else {
        -not $operationProfile.RequireFreeAdminPort
    }
    $shouldSkipBootstrapAdmin = $SkipBootstrapAdmin.IsPresent -or (-not $operationProfile.IncludeBootstrapAdmin)

    Write-Host "Running installer preflight checks for $($operationProfile.DisplayName) mode..."
    $preflightIssues = @(Get-InstallerPreflightValidationIssues `
        -Configuration $configuration `
        -SkipPortAvailabilityCheck:$shouldSkipPortAvailabilityCheck `
        -SkipBootstrapAdmin:$shouldSkipBootstrapAdmin)

    if ($preflightIssues.Count -gt 0) {
        $report = New-InstallerExecutionReport `
            -Manifest $manifest `
            -OperationProfile $operationProfile `
            -Configuration $configuration `
            -ValidationIssues $preflightIssues
        throw ([string]::Join([Environment]::NewLine, @($preflightIssues | ForEach-Object { Format-InstallerValidationIssue -Issue $_ })))
    }

    if ($PreflightOnly) {
        $report = New-InstallerExecutionReport `
            -Manifest $manifest `
            -OperationProfile $operationProfile `
            -Configuration $configuration

        Write-Host "Preflight checks completed successfully for $($operationProfile.DisplayName) mode."
        return
    }

    $executionPlan = New-InstallerExecutionPlan `
        -Configuration $configuration `
        -Mode $Mode `
        -SkipImageBuild:$SkipImageBuild `
        -SkipBootstrap:$SkipBootstrap `
        -SkipBootstrapAdmin:$shouldSkipBootstrapAdmin

    $stepResults = Invoke-InstallerExecutionPlan -ExecutionPlan $executionPlan -CommandRunner $commandRunner
    if (-not $DryRun) {
        $runtimeDiagnostics = Get-InstallerRuntimeDiagnostics -Configuration $configuration -CommandRunner $commandRunner
    }

    $failedStep = $stepResults | Where-Object Status -eq 'failed' | Select-Object -First 1
    $report = New-InstallerExecutionReport `
        -Manifest $manifest `
        -OperationProfile $operationProfile `
        -Configuration $configuration `
        -StepResults $stepResults `
        -RuntimeDiagnostics $runtimeDiagnostics `
        -FailureMessage $(if ($null -eq $failedStep) { $null } else { $failedStep.Message })

    if ($null -ne $failedStep) {
        throw $failedStep.Message
    }

    if ($report.Outcome -eq 'degraded') {
        Write-Warning 'Installer completed with degraded diagnostics. Review runtime status, worker diagnostics and troubleshooting hints in the JSON report or console output.'
    }

    Write-Host "$($operationProfile.DisplayName.Substring(0,1).ToString().ToUpperInvariant() + $operationProfile.DisplayName.Substring(1)) flow completed. Admin UI should be available on https://<host>:$($configuration.AdminHttpsPort)/ after runtime startup stabilizes."
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($ReportJsonPath) -and $null -ne $report) {
        Write-InstallerExecutionReportJson -Report $report -Path $ReportJsonPath
    }
}
