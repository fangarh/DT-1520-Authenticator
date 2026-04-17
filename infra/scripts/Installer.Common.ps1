Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Installer.Contract.ps1')
. (Join-Path $PSScriptRoot 'Installer.Diagnostics.ps1')

function Read-InstallerEnvironmentFile {
    param([Parameter(Mandatory)][string]$Path)

    $values = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmedLine = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmedLine) -or $trimmedLine.StartsWith('#')) {
            continue
        }

        $separatorIndex = $trimmedLine.IndexOf('=')
        if ($separatorIndex -lt 1) {
            throw "Invalid env line '$trimmedLine'. Expected KEY=VALUE."
        }

        $key = $trimmedLine.Substring(0, $separatorIndex).Trim()
        $value = $trimmedLine.Substring($separatorIndex + 1)
        $values[$key] = $value
    }

    return $values
}

function Get-InstallerRequiredEnvironmentKeys {
    return @(
        'ConnectionStrings__Postgres',
        'BootstrapOAuth__CurrentSigningKeyId',
        'BootstrapOAuth__CurrentSigningKey',
        'TotpProtection__CurrentKeyVersion',
        'TotpProtection__CurrentKey',
        'OTPAUTH_TLS_CERT_PATH',
        'OTPAUTH_TLS_KEY_PATH'
    )
}

function Test-InstallerPathWithinRoot {
    param(
        [Parameter(Mandatory)][string]$RootPath,
        [Parameter(Mandatory)][string]$ChildPath
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($RootPath)
    $childFullPath = [System.IO.Path]::GetFullPath($ChildPath)

    if (-not $rootFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $rootFullPath = "$rootFullPath$([System.IO.Path]::DirectorySeparatorChar)"
    }

    return $childFullPath.StartsWith($rootFullPath, [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-InstallerWritableDirectory {
    param([Parameter(Mandatory)][string]$DirectoryPath)

    $probePath = Join-Path $DirectoryPath ".installer-write-probe-$([guid]::NewGuid().ToString('N')).tmp"
    try {
        Set-Content -LiteralPath $probePath -Value 'probe' -Encoding Ascii
        Remove-Item -LiteralPath $probePath -Force
        return $true
    }
    catch {
        return $false
    }
}

function Test-InstallerPortAvailable {
    param([Parameter(Mandatory)][int]$Port)

    $listener = $null
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, $Port)
        $listener.Start()
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $listener) {
            $listener.Stop()
        }
    }
}

function Get-InstallerConfigurationValidationIssues {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$ComposeFilePath,
        [Parameter(Mandatory)][string]$EnvFilePath,
        [Parameter(Mandatory)][string]$BootstrapAdminUsername,
        [Parameter(Mandatory)][string[]]$BootstrapAdminPermissions
    )

    $issues = New-Object System.Collections.Generic.List[object]
    $repositoryRootFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $composeFileFullPath = [System.IO.Path]::GetFullPath($ComposeFilePath)
    $envFileFullPath = [System.IO.Path]::GetFullPath($EnvFilePath)

    if (-not (Test-Path -LiteralPath $composeFileFullPath -PathType Leaf)) {
        $issues.Add((New-InstallerValidationIssue -Code 'compose_file_not_found' -Field 'ComposeFilePath' -Message "Compose file '$composeFileFullPath' was not found."))
    }

    if (-not (Test-Path -LiteralPath $envFileFullPath -PathType Leaf)) {
        $issues.Add((New-InstallerValidationIssue -Code 'env_file_not_found' -Field 'EnvFilePath' -Message "Env file '$envFileFullPath' was not found."))
        return $issues.ToArray()
    }

    if (Test-InstallerPathWithinRoot -RootPath $repositoryRootFullPath -ChildPath $envFileFullPath) {
        $issues.Add((New-InstallerValidationIssue -Code 'env_file_inside_repository' -Field 'EnvFilePath' -Message "Env file '$envFileFullPath' must live outside the repository root '$repositoryRootFullPath'."))
    }

    $environmentValues = $null
    try {
        $environmentValues = Read-InstallerEnvironmentFile -Path $envFileFullPath
    }
    catch {
        $issues.Add((New-InstallerValidationIssue -Code 'env_file_invalid_format' -Field 'EnvFilePath' -Message $_.Exception.Message))
        return $issues.ToArray()
    }

    foreach ($requiredKey in Get-InstallerRequiredEnvironmentKeys) {
        if (-not $environmentValues.Contains($requiredKey) -or [string]::IsNullOrWhiteSpace($environmentValues[$requiredKey])) {
            $issues.Add((New-InstallerValidationIssue -Code 'env_key_missing' -Field $requiredKey -Message "Env file must contain a non-empty '$requiredKey' value."))
        }
    }

    $tlsCertPath = $environmentValues['OTPAUTH_TLS_CERT_PATH']
    if (-not [string]::IsNullOrWhiteSpace($tlsCertPath) -and -not (Test-Path -LiteralPath $tlsCertPath -PathType Leaf)) {
        $issues.Add((New-InstallerValidationIssue -Code 'tls_certificate_not_found' -Field 'OTPAUTH_TLS_CERT_PATH' -Message "TLS certificate file '$tlsCertPath' was not found."))
    }

    $tlsKeyPath = $environmentValues['OTPAUTH_TLS_KEY_PATH']
    if (-not [string]::IsNullOrWhiteSpace($tlsKeyPath) -and -not (Test-Path -LiteralPath $tlsKeyPath -PathType Leaf)) {
        $issues.Add((New-InstallerValidationIssue -Code 'tls_key_not_found' -Field 'OTPAUTH_TLS_KEY_PATH' -Message "TLS private key file '$tlsKeyPath' was not found."))
    }

    $adminPort = 8443
    if ($environmentValues.Contains('OTPAUTH_ADMIN_HTTPS_PORT') -and -not [string]::IsNullOrWhiteSpace($environmentValues['OTPAUTH_ADMIN_HTTPS_PORT'])) {
        if (-not [int]::TryParse($environmentValues['OTPAUTH_ADMIN_HTTPS_PORT'], [ref]$adminPort)) {
            $issues.Add((New-InstallerValidationIssue -Code 'admin_https_port_invalid' -Field 'OTPAUTH_ADMIN_HTTPS_PORT' -Message 'OTPAUTH_ADMIN_HTTPS_PORT must be a valid integer.'))
        }
    }

    if ($adminPort -lt 1 -or $adminPort -gt 65535) {
        $issues.Add((New-InstallerValidationIssue -Code 'admin_https_port_out_of_range' -Field 'OTPAUTH_ADMIN_HTTPS_PORT' -Message 'OTPAUTH_ADMIN_HTTPS_PORT must be between 1 and 65535.'))
    }

    if ([string]::IsNullOrWhiteSpace($BootstrapAdminUsername)) {
        $issues.Add((New-InstallerValidationIssue -Code 'bootstrap_admin_username_missing' -Field 'BootstrapAdminUsername' -Message 'Bootstrap admin username must be a non-empty value.'))
    }

    if ($BootstrapAdminPermissions.Count -eq 0) {
        $issues.Add((New-InstallerValidationIssue -Code 'bootstrap_admin_permissions_missing' -Field 'BootstrapAdminPermissions' -Message 'At least one bootstrap admin permission is required.'))
    }
    else {
        for ($index = 0; $index -lt $BootstrapAdminPermissions.Count; $index++) {
            if ([string]::IsNullOrWhiteSpace($BootstrapAdminPermissions[$index])) {
                $issues.Add((New-InstallerValidationIssue -Code 'bootstrap_admin_permission_invalid' -Field "BootstrapAdminPermissions[$index]" -Message 'Bootstrap admin permissions must be non-empty values.'))
            }
        }
    }

    return $issues.ToArray()
}

function New-InstallerConfiguration {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$ComposeFilePath,
        [Parameter(Mandatory)][string]$EnvFilePath,
        [Parameter(Mandatory)]$EnvironmentValues,
        [Parameter(Mandatory)][string]$BootstrapAdminUsername,
        [Parameter(Mandatory)][string[]]$BootstrapAdminPermissions
    )

    $adminPort = 8443
    if ($EnvironmentValues.Contains('OTPAUTH_ADMIN_HTTPS_PORT') -and -not [string]::IsNullOrWhiteSpace($EnvironmentValues['OTPAUTH_ADMIN_HTTPS_PORT'])) {
        [int]::TryParse($EnvironmentValues['OTPAUTH_ADMIN_HTTPS_PORT'], [ref]$adminPort) | Out-Null
    }

    return [pscustomobject]@{
        RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
        ComposeFilePath = [System.IO.Path]::GetFullPath($ComposeFilePath)
        EnvFilePath = [System.IO.Path]::GetFullPath($EnvFilePath)
        EnvDirectoryPath = Split-Path -Parent ([System.IO.Path]::GetFullPath($EnvFilePath))
        EnvironmentValues = $EnvironmentValues
        AdminHttpsPort = $adminPort
        BootstrapAdminUsername = $BootstrapAdminUsername
        BootstrapAdminPermissions = @($BootstrapAdminPermissions)
    }
}

function Get-InstallerConfiguration {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$ComposeFilePath,
        [Parameter(Mandatory)][string]$EnvFilePath,
        [Parameter(Mandatory)][string]$BootstrapAdminUsername,
        [Parameter(Mandatory)][string[]]$BootstrapAdminPermissions
    )

    $issues = @(Get-InstallerConfigurationValidationIssues `
        -RepositoryRoot $RepositoryRoot `
        -ComposeFilePath $ComposeFilePath `
        -EnvFilePath $EnvFilePath `
        -BootstrapAdminUsername $BootstrapAdminUsername `
        -BootstrapAdminPermissions $BootstrapAdminPermissions)

    if ($issues.Count -gt 0) {
        throw ([string]::Join([Environment]::NewLine, @($issues | ForEach-Object { Format-InstallerValidationIssue -Issue $_ })))
    }

    $environmentValues = Read-InstallerEnvironmentFile -Path $EnvFilePath
    return New-InstallerConfiguration `
        -RepositoryRoot $RepositoryRoot `
        -ComposeFilePath $ComposeFilePath `
        -EnvFilePath $EnvFilePath `
        -EnvironmentValues $environmentValues `
        -BootstrapAdminUsername $BootstrapAdminUsername `
        -BootstrapAdminPermissions $BootstrapAdminPermissions
}

function Get-DockerComposeBaseArguments {
    param([Parameter(Mandatory)]$Configuration)

    return @(
        'compose',
        '--env-file', $Configuration.EnvFilePath,
        '-f', $Configuration.ComposeFilePath
    )
}

function Get-InstallerOperationProfile {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Install', 'Update', 'Recover')]
        [string]$Mode
    )

    switch ($Mode) {
        'Install' {
            return [pscustomobject]@{
                Mode = 'Install'
                DisplayName = 'install'
                IncludeImageBuild = $true
                IncludeBootstrap = $true
                IncludeBootstrapAdmin = $true
                RequireFreeAdminPort = $true
            }
        }
        'Update' {
            return [pscustomobject]@{
                Mode = 'Update'
                DisplayName = 'update'
                IncludeImageBuild = $true
                IncludeBootstrap = $true
                IncludeBootstrapAdmin = $false
                RequireFreeAdminPort = $false
            }
        }
        'Recover' {
            return [pscustomobject]@{
                Mode = 'Recover'
                DisplayName = 'recovery'
                IncludeImageBuild = $false
                IncludeBootstrap = $false
                IncludeBootstrapAdmin = $false
                RequireFreeAdminPort = $false
            }
        }
    }
}

function New-InstallerExecutionPlan {
    param(
        [Parameter(Mandatory)]$Configuration,
        [Parameter(Mandatory)][ValidateSet('Install', 'Update', 'Recover')][string]$Mode,
        [switch]$SkipImageBuild,
        [switch]$SkipBootstrap,
        [switch]$SkipBootstrapAdmin
    )

    $baseArguments = Get-DockerComposeBaseArguments -Configuration $Configuration
    $operationProfile = Get-InstallerOperationProfile -Mode $Mode
    $includeImageBuild = $operationProfile.IncludeImageBuild -and (-not $SkipImageBuild)
    $includeBootstrap = $operationProfile.IncludeBootstrap -and (-not $SkipBootstrap)
    $includeBootstrapAdmin = $operationProfile.IncludeBootstrapAdmin -and (-not $SkipBootstrapAdmin)
    $steps = New-Object System.Collections.Generic.List[object]

    if ($includeImageBuild) {
        $steps.Add((New-InstallerPlanStep -Id 'build_images' -Name 'Build images' -FilePath 'docker' -Arguments @($baseArguments + @('build', 'api', 'worker', 'admin', 'bootstrap')) -EnvironmentOverrides @{} -CaptureOutput:$false))
    }

    $steps.Add((New-InstallerPlanStep -Id 'start_infrastructure_dependencies' -Name 'Start infrastructure dependencies' -FilePath 'docker' -Arguments @($baseArguments + @('up', '-d', '--wait', 'postgres', 'redis')) -EnvironmentOverrides @{} -CaptureOutput:$false))

    if ($includeBootstrap) {
        $steps.Add((New-InstallerPlanStep -Id 'ensure_database' -Name 'Ensure database' -FilePath 'docker' -Arguments @($baseArguments + @('--profile', 'bootstrap', 'run', '--rm', 'bootstrap', 'ensure-database')) -EnvironmentOverrides @{} -CaptureOutput:$false))
        $steps.Add((New-InstallerPlanStep -Id 'apply_migrations' -Name 'Apply migrations' -FilePath 'docker' -Arguments @($baseArguments + @('--profile', 'bootstrap', 'run', '--rm', 'bootstrap', 'migrate')) -EnvironmentOverrides @{} -CaptureOutput:$false))
    }

    if ($includeBootstrapAdmin) {
        $steps.Add((New-InstallerPlanStep -Id 'upsert_bootstrap_admin_user' -Name 'Upsert bootstrap admin user' -FilePath 'docker' -Arguments @($baseArguments + @('--profile', 'bootstrap', 'run', '--rm', 'bootstrap', 'upsert-admin-user', $Configuration.BootstrapAdminUsername) + $Configuration.BootstrapAdminPermissions) -EnvironmentOverrides @{ OTPAUTH_ADMIN_PASSWORD = [Environment]::GetEnvironmentVariable('OTPAUTH_ADMIN_PASSWORD', 'Process') } -CaptureOutput:$false))
    }

    $steps.Add((New-InstallerPlanStep -Id 'start_runtime_services' -Name 'Start runtime services' -FilePath 'docker' -Arguments @($baseArguments + @('up', '-d', '--wait', 'api', 'admin')) -EnvironmentOverrides @{} -CaptureOutput:$false))
    $steps.Add((New-InstallerPlanStep -Id 'start_worker' -Name 'Start worker' -FilePath 'docker' -Arguments @($baseArguments + @('up', '-d', '--wait', 'worker')) -EnvironmentOverrides @{} -CaptureOutput:$false))
    $steps.Add((New-InstallerPlanStep -Id 'show_runtime_status' -Name 'Show runtime status' -FilePath 'docker' -Arguments @($baseArguments + @('ps')) -EnvironmentOverrides @{} -CaptureOutput:$true))

    return $steps.ToArray()
}

function Get-InstallerPreflightValidationIssues {
    param(
        [Parameter(Mandatory)]$Configuration,
        [switch]$SkipPortAvailabilityCheck,
        [switch]$SkipBootstrapAdmin
    )

    $issues = New-Object System.Collections.Generic.List[object]
    $dockerCommand = Get-Command 'docker' -ErrorAction SilentlyContinue
    if ($null -eq $dockerCommand) {
        $issues.Add((New-InstallerValidationIssue -Code 'docker_cli_not_found' -Field 'docker' -Message 'Docker CLI was not found in PATH.'))
        return $issues.ToArray()
    }

    if (-not (Test-InstallerWritableDirectory -DirectoryPath $Configuration.EnvDirectoryPath)) {
        $issues.Add((New-InstallerValidationIssue -Code 'env_directory_not_writable' -Field 'EnvDirectoryPath' -Message "Directory '$($Configuration.EnvDirectoryPath)' is not writable."))
    }

    if (-not $SkipPortAvailabilityCheck -and -not (Test-InstallerPortAvailable -Port $Configuration.AdminHttpsPort)) {
        $issues.Add((New-InstallerValidationIssue -Code 'admin_https_port_in_use' -Field 'OTPAUTH_ADMIN_HTTPS_PORT' -Message "Port $($Configuration.AdminHttpsPort) is already in use."))
    }

    if (-not $SkipBootstrapAdmin -and [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('OTPAUTH_ADMIN_PASSWORD', 'Process'))) {
        $issues.Add((New-InstallerValidationIssue -Code 'bootstrap_admin_password_missing' -Field 'OTPAUTH_ADMIN_PASSWORD' -Message "Environment variable 'OTPAUTH_ADMIN_PASSWORD' must be present in process scope for bootstrap admin creation."))
    }

    try {
        & docker compose version *> $null
        if ($LASTEXITCODE -ne 0) {
            $issues.Add((New-InstallerValidationIssue -Code 'docker_compose_version_failed' -Field 'docker compose version' -Message "Command 'docker compose version' failed with exit code $LASTEXITCODE."))
        }
    }
    catch {
        $issues.Add((New-InstallerValidationIssue -Code 'docker_compose_version_failed' -Field 'docker compose version' -Message $_.Exception.Message))
    }

    try {
        & docker @((Get-DockerComposeBaseArguments -Configuration $Configuration) + @('config', '--quiet')) *> $null
        if ($LASTEXITCODE -ne 0) {
            $issues.Add((New-InstallerValidationIssue -Code 'docker_compose_config_failed' -Field 'docker compose config --quiet' -Message "Command 'docker compose config --quiet' failed with exit code $LASTEXITCODE."))
        }
    }
    catch {
        $issues.Add((New-InstallerValidationIssue -Code 'docker_compose_config_failed' -Field 'docker compose config --quiet' -Message $_.Exception.Message))
    }

    return $issues.ToArray()
}

function Invoke-InstallerExecutionPlan {
    param(
        [Parameter(Mandatory)][object[]]$ExecutionPlan,
        [Parameter(Mandatory)][scriptblock]$CommandRunner
    )

    $results = New-Object System.Collections.Generic.List[object]
    foreach ($step in $ExecutionPlan) {
        Write-Host "Running step: $($step.Name)"
        $startedUtc = [DateTime]::UtcNow
        try {
            $commandResult = & $CommandRunner $step.FilePath $step.Arguments $step.EnvironmentOverrides $step.CaptureOutput
            $completedUtc = [DateTime]::UtcNow
            $results.Add((New-InstallerStepResult -Step $step -Status 'succeeded' -StartedUtc $startedUtc -CompletedUtc $completedUtc -Message 'Step completed successfully.' -ExitCode ([int]$commandResult.ExitCode) -OutputLines @($commandResult.OutputLines)))
        }
        catch {
            $completedUtc = [DateTime]::UtcNow
            $exitCode = if ($_.Exception.Data.Contains('InstallerExitCode')) {
                [int]$_.Exception.Data['InstallerExitCode']
            }
            else {
                1
            }
            $outputLines = if ($_.Exception.Data.Contains('InstallerOutputLines')) {
                @($_.Exception.Data['InstallerOutputLines'])
            }
            else {
                @()
            }

            $results.Add((New-InstallerStepResult -Step $step -Status 'failed' -StartedUtc $startedUtc -CompletedUtc $completedUtc -Message $_.Exception.Message -ExitCode $exitCode -OutputLines $outputLines))
            break
        }
    }

    return $results.ToArray()
}

function Invoke-InstallerCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][hashtable]$EnvironmentOverrides,
        [bool]$CaptureOutput
    )

    $previousValues = @{}
    $outputLines = New-Object System.Collections.Generic.List[string]
    try {
        foreach ($entry in $EnvironmentOverrides.GetEnumerator()) {
            $previousValues[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        }

        if ($CaptureOutput) {
            $commandOutput = & $FilePath @Arguments 2>&1
            foreach ($outputLine in $commandOutput) {
                $formattedLine = $outputLine.ToString()
                $outputLines.Add($formattedLine)
                Write-Host $formattedLine
            }
        }
        else {
            & $FilePath @Arguments
        }

        $exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }
        if ($exitCode -ne 0) {
            $exception = [System.InvalidOperationException]::new("Command '$FilePath $($Arguments -join ' ')' failed with exit code $exitCode.")
            $exception.Data['InstallerExitCode'] = $exitCode
            $exception.Data['InstallerOutputLines'] = @($outputLines)
            throw $exception
        }

        return [pscustomobject]@{
            ExitCode = $exitCode
            OutputLines = @($outputLines)
        }
    }
    catch {
        if (-not $_.Exception.Data.Contains('InstallerExitCode')) {
            $_.Exception.Data['InstallerExitCode'] = if ($null -eq $LASTEXITCODE) { 1 } else { $LASTEXITCODE }
        }

        if (-not $_.Exception.Data.Contains('InstallerOutputLines')) {
            $_.Exception.Data['InstallerOutputLines'] = @($outputLines)
        }

        throw
    }
    finally {
        foreach ($entry in $EnvironmentOverrides.GetEnumerator()) {
            [Environment]::SetEnvironmentVariable($entry.Key, $previousValues[$entry.Key], 'Process')
        }
    }
}
