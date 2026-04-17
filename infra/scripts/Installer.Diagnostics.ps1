Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-InstallerDiagnosticCommandRunner {
    param(
        [Parameter(Mandatory)][scriptblock]$CommandRunner,
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][hashtable]$EnvironmentOverrides,
        [Parameter(Mandatory)][bool]$CaptureOutput
    )

    return $CommandRunner.InvokeReturnAsIs($FilePath, $Arguments, $EnvironmentOverrides, $CaptureOutput)
}

function ConvertFrom-InstallerComposeStatusJson {
    param([string[]]$OutputLines)

    $json = [string]::Join([Environment]::NewLine, @($OutputLines))
    if ([string]::IsNullOrWhiteSpace($json)) {
        return @()
    }

    try {
        $services = $json | ConvertFrom-Json
    }
    catch {
        throw "Unable to parse docker compose runtime status JSON. $($_.Exception.Message)"
    }

    if ($services -isnot [System.Collections.IEnumerable] -or $services -is [string]) {
        $services = @($services)
    }

    return @($services | ForEach-Object {
        [pscustomobject]@{
            Service = $_.Service
            State = $_.State
            Health = if ([string]::IsNullOrWhiteSpace($_.Health)) { $null } else { $_.Health }
            ExitCode = if ($null -eq $_.ExitCode) { $null } else { [int]$_.ExitCode }
            Publishers = @($_.Publishers | ForEach-Object {
                [pscustomobject]@{
                    URL = $_.URL
                    PublishedPort = if ($null -eq $_.PublishedPort) { $null } else { [int]$_.PublishedPort }
                    TargetPort = if ($null -eq $_.TargetPort) { $null } else { [int]$_.TargetPort }
                    Protocol = $_.Protocol
                }
            })
        }
    })
}

function ConvertFrom-InstallerWorkerHeartbeatJson {
    param([string[]]$OutputLines)

    $json = [string]::Join([Environment]::NewLine, @($OutputLines))
    if ([string]::IsNullOrWhiteSpace($json)) {
        throw 'Worker heartbeat output is empty.'
    }

    try {
        $snapshot = $json | ConvertFrom-Json
    }
    catch {
        throw "Unable to parse worker heartbeat JSON. $($_.Exception.Message)"
    }

    return [pscustomobject]@{
        ServiceName = $snapshot.ServiceName
        StartedAtUtc = $snapshot.StartedAtUtc
        LastHeartbeatUtc = $snapshot.LastHeartbeatUtc
        LastExecutionStartedUtc = $snapshot.LastExecutionStartedUtc
        LastExecutionCompletedUtc = $snapshot.LastExecutionCompletedUtc
        ExecutionOutcome = $snapshot.ExecutionOutcome
        ConsecutiveFailureCount = [int]$snapshot.ConsecutiveFailureCount
        DependencyStatuses = @($snapshot.DependencyStatuses | ForEach-Object {
            [pscustomobject]@{
                Name = $_.Name
                Status = $_.Status
                CheckedAtUtc = $_.CheckedAtUtc
                FailureKind = $_.FailureKind
            }
        })
        JobStatuses = @($snapshot.JobStatuses | ForEach-Object {
            [pscustomobject]@{
                Name = $_.Name
                Status = $_.Status
                IntervalSeconds = [int]$_.IntervalSeconds
                IsDue = [bool]$_.IsDue
                LastStartedAtUtc = $_.LastStartedAtUtc
                LastCompletedAtUtc = $_.LastCompletedAtUtc
                LastSuccessfulCompletedAtUtc = $_.LastSuccessfulCompletedAtUtc
                SuccessfulRunCount = [int]$_.SuccessfulRunCount
                FailedRunCount = [int]$_.FailedRunCount
                ConsecutiveFailureCount = [int]$_.ConsecutiveFailureCount
                LastSummary = $_.LastSummary
                FailureKind = $_.FailureKind
                LastMetrics = @($_.LastMetrics | ForEach-Object {
                    [pscustomobject]@{
                        Name = $_.Name
                        Value = [long]$_.Value
                    }
                })
            }
        })
    }
}

function Get-InstallerTroubleshootingHints {
    param(
        [object[]]$RuntimeServices = @(),
        $WorkerHeartbeat = $null,
        [object[]]$DiagnosticIssues = @()
    )

    $hints = New-Object System.Collections.Generic.List[object]

    foreach ($service in $RuntimeServices) {
        $serviceName = $service.Service
        if ([string]::IsNullOrWhiteSpace($serviceName)) {
            continue
        }

        if (-not [string]::Equals($service.State, 'running', [System.StringComparison]::OrdinalIgnoreCase)) {
            $hints.Add((New-InstallerTroubleshootingHint `
                    -Code 'service_not_running' `
                    -Severity 'error' `
                    -Service $serviceName `
                    -Component 'runtime' `
                    -Message "Runtime service '$serviceName' is in state '$($service.State)'." `
                    -RecommendedAction "Inspect 'docker compose logs --tail 100 $serviceName' and verify the service dependencies are available."))
        }

        if (-not [string]::IsNullOrWhiteSpace($service.Health) -and -not [string]::Equals($service.Health, 'healthy', [System.StringComparison]::OrdinalIgnoreCase)) {
            $severity = if ([string]::Equals($service.Health, 'starting', [System.StringComparison]::OrdinalIgnoreCase)) {
                'warning'
            }
            else {
                'error'
            }

            $hints.Add((New-InstallerTroubleshootingHint `
                    -Code 'service_health_not_healthy' `
                    -Severity $severity `
                    -Service $serviceName `
                    -Component 'runtime' `
                    -Message "Runtime service '$serviceName' reported health '$($service.Health)'." `
                    -RecommendedAction "Inspect 'docker compose ps $serviceName' and 'docker compose logs --tail 100 $serviceName' before retrying recovery."))
        }
    }

    $workerService = @($RuntimeServices | Where-Object Service -eq 'worker' | Select-Object -First 1)
    if ($workerService.Count -gt 0 -and $null -eq $WorkerHeartbeat) {
        $hints.Add((New-InstallerTroubleshootingHint `
                -Code 'worker_heartbeat_unavailable' `
                -Severity 'warning' `
                -Service 'worker' `
                -Component 'worker_heartbeat' `
                -Message 'Worker container is present but installer could not read the sanitized heartbeat snapshot.' `
                -RecommendedAction "Verify '/tmp/otpauth-worker/heartbeat.json' inside the worker container and inspect 'docker compose logs --tail 100 worker'."))
    }

    if ($null -ne $WorkerHeartbeat) {
        if (-not [string]::Equals($WorkerHeartbeat.ExecutionOutcome, 'healthy', [System.StringComparison]::OrdinalIgnoreCase)) {
            $hints.Add((New-InstallerTroubleshootingHint `
                    -Code 'worker_execution_degraded' `
                    -Severity 'warning' `
                    -Service 'worker' `
                    -Component 'worker_execution' `
                    -Message "Worker execution outcome is '$($WorkerHeartbeat.ExecutionOutcome)' with consecutive failure count $($WorkerHeartbeat.ConsecutiveFailureCount)." `
                    -RecommendedAction "Inspect worker dependency statuses and job diagnostics before retrying recovery."))
        }

        foreach ($dependencyStatus in $WorkerHeartbeat.DependencyStatuses) {
            if ([string]::Equals($dependencyStatus.Status, 'healthy', [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $hints.Add((New-InstallerTroubleshootingHint `
                    -Code 'worker_dependency_degraded' `
                    -Severity 'error' `
                    -Service 'worker' `
                    -Component $dependencyStatus.Name `
                    -FailureKind $dependencyStatus.FailureKind `
                    -Message "Worker dependency '$($dependencyStatus.Name)' is '$($dependencyStatus.Status)'." `
                    -RecommendedAction "Treat this as a dependency incident and validate reachability/auth for '$($dependencyStatus.Name)' from the worker container."))
        }

        foreach ($jobStatus in $WorkerHeartbeat.JobStatuses) {
            switch ($jobStatus.Status) {
                'blocked' {
                    $hints.Add((New-InstallerTroubleshootingHint `
                            -Code 'worker_job_blocked' `
                            -Severity 'warning' `
                            -Service 'worker' `
                            -Component $jobStatus.Name `
                            -FailureKind $jobStatus.FailureKind `
                            -Message "Worker job '$($jobStatus.Name)' is blocked." `
                            -RecommendedAction "Check dependency probes first; blocked jobs usually mean the scheduler is alive but dependencies are not ready."))
                }
                'degraded' {
                    $hints.Add((New-InstallerTroubleshootingHint `
                            -Code 'worker_job_degraded' `
                            -Severity 'warning' `
                            -Service 'worker' `
                            -Component $jobStatus.Name `
                            -FailureKind $jobStatus.FailureKind `
                            -Message "Worker job '$($jobStatus.Name)' is degraded." `
                            -RecommendedAction "Inspect worker logs and the job summary/metrics in the heartbeat snapshot before retrying the job path."))
                }
            }
        }
    }

    foreach ($issue in $DiagnosticIssues) {
        $hints.Add((New-InstallerTroubleshootingHint `
                -Code 'installer_diagnostic_issue' `
                -Severity 'warning' `
                -Service $issue.Source `
                -Component $issue.Code `
                -Message $issue.Message `
                -RecommendedAction 'Use the runtime status lines and direct docker compose commands to complete manual troubleshooting.'))
    }

    return $hints.ToArray()
}

function Get-InstallerRuntimeDiagnostics {
    param(
        [Parameter(Mandatory)]$Configuration,
        [Parameter(Mandatory)][scriptblock]$CommandRunner
    )

    $diagnosticIssues = New-Object System.Collections.Generic.List[object]
    $services = @()
    $workerHeartbeat = $null
    $baseArguments = Get-DockerComposeBaseArguments -Configuration $Configuration
    $environmentOverrides = @{}
    $captureOutput = $true

    try {
        $statusArguments = @($baseArguments + @('ps', '--format', 'json'))
        $statusResult = Invoke-InstallerDiagnosticCommandRunner `
            -CommandRunner $CommandRunner `
            -FilePath 'docker' `
            -Arguments $statusArguments `
            -EnvironmentOverrides $environmentOverrides `
            -CaptureOutput $captureOutput
        $services = @(ConvertFrom-InstallerComposeStatusJson -OutputLines $statusResult.OutputLines)
    }
    catch {
        $diagnosticIssues.Add((New-InstallerDiagnosticIssue `
                -Source 'docker compose ps --format json' `
                -Code 'runtime_status_unavailable' `
                -Message $_.Exception.Message))
    }

    $shouldReadWorkerHeartbeat = @($services | Where-Object Service -eq 'worker').Count -gt 0
    if ($shouldReadWorkerHeartbeat) {
        try {
            $heartbeatArguments = @($baseArguments + @('exec', '-T', 'worker', '/bin/sh', '-c', 'cat /tmp/otpauth-worker/heartbeat.json'))
            $heartbeatResult = Invoke-InstallerDiagnosticCommandRunner `
                -CommandRunner $CommandRunner `
                -FilePath 'docker' `
                -Arguments $heartbeatArguments `
                -EnvironmentOverrides $environmentOverrides `
                -CaptureOutput $captureOutput
            $workerHeartbeat = ConvertFrom-InstallerWorkerHeartbeatJson -OutputLines $heartbeatResult.OutputLines
        }
        catch {
            $diagnosticIssues.Add((New-InstallerDiagnosticIssue `
                    -Source 'worker heartbeat' `
                    -Code 'worker_heartbeat_unavailable' `
                    -Message $_.Exception.Message))
        }
    }

    $runtimeServices = @($services)
    $diagnosticIssueArray = $diagnosticIssues.ToArray()
    $troubleshootingHints = @(Get-InstallerTroubleshootingHints `
            -RuntimeServices $runtimeServices `
            -WorkerHeartbeat $workerHeartbeat `
            -DiagnosticIssues $diagnosticIssueArray)

    return [pscustomobject]@{
        Services = $runtimeServices
        Worker = $workerHeartbeat
        DiagnosticIssues = $diagnosticIssueArray
        TroubleshootingHints = $troubleshootingHints
    }
}
