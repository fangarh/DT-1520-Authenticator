Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendRoot = Split-Path -Parent $scriptRoot
$solutionPath = Join-Path $backendRoot 'OtpAuth.slnx'
$testProjectPaths = @(
    Join-Path $backendRoot 'OtpAuth.Infrastructure.Tests\OtpAuth.Infrastructure.Tests.csproj'
    Join-Path $backendRoot 'OtpAuth.Worker.Tests\OtpAuth.Worker.Tests.csproj'
)

Push-Location $backendRoot
try {
    Write-Host 'Building backend solution sequentially...'
    dotnet build $solutionPath -p:BuildInParallel=false -p:RestoreBuildInParallel=false -maxcpucount:1

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    foreach ($testProjectPath in $testProjectPaths) {
        Write-Host "Running tests sequentially for $testProjectPath..."
        dotnet test $testProjectPath -p:BuildInParallel=false -p:RestoreBuildInParallel=false -maxcpucount:1

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet test failed for '$testProjectPath' with exit code $LASTEXITCODE."
        }
    }
}
finally {
    Pop-Location
}
