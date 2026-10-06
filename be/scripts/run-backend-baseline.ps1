param(
    [switch]$MeasurementsOnly
)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput = Join-Path $taskRoot 'be/artifacts/baseline'
$taskProject = Join-Path $taskRoot 'be/tests/MealTrace.Api.Tests/MealTrace.Api.Tests.csproj'
$taskPrevious = @{}
foreach ($taskName in @('MEALTRACE_TEST_CONNECTION', 'MEALTRACE_RUN_BASELINE', 'MEALTRACE_BASELINE_OUTPUT')) {
    $taskPrevious[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
}

try {
    if ([string]::IsNullOrWhiteSpace($env:MEALTRACE_TEST_CONNECTION)) {
        $taskConfigPath = Join-Path $taskRoot 'be/src/MealTrace.Api/appsettings.Development.local.json'
        if (-not (Test-Path -LiteralPath $taskConfigPath)) {
            throw 'Set MEALTRACE_TEST_CONNECTION or configure the ignored local database config first.'
        }
        $taskConfig = Get-Content -LiteralPath $taskConfigPath -Raw | ConvertFrom-Json
        $env:MEALTRACE_TEST_CONNECTION = $taskConfig.ConnectionStrings.MealTrace
    }
    if ([string]::IsNullOrWhiteSpace($env:MEALTRACE_TEST_CONNECTION)) { throw 'PostgreSQL connection is missing.' }
    $env:MEALTRACE_RUN_BASELINE = '1'
    $env:MEALTRACE_BASELINE_OUTPUT = $taskOutput
    $taskArguments = @('test', $taskProject, '--logger', 'trx;LogFileName=backend-baseline.trx',
        '--results-directory', $taskOutput, '--verbosity', 'minimal')
    if ($MeasurementsOnly) { $taskArguments += @('--filter', 'FullyQualifiedName~BackendBaselineTests') }
    & dotnet @taskArguments
    if ($LASTEXITCODE -ne 0) { throw "Backend baseline failed (exit $LASTEXITCODE). See the test output." }
    Write-Host "Baseline saved to $taskOutput"
}
finally {
    foreach ($taskName in $taskPrevious.Keys) {
        [Environment]::SetEnvironmentVariable($taskName, $taskPrevious[$taskName], 'Process')
    }
}
