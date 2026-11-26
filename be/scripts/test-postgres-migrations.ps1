# Rehearse EF migrations against two disposable databases in a private PostgreSQL 18 cluster.
# Requires PostgreSQL 18 utilities and the .NET 8 SDK with dotnet-ef on PATH.
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PostgresBin,
    [string]$CandidateRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$BaselineRoot = $CandidateRoot,
    [ValidatePattern('^\d{14}_[A-Za-z0-9_]+$')]
    [string]$BaselineMigration = '20261028090000_KitchenV13Nutrition'
)

$ErrorActionPreference = 'Stop'
$CandidateRoot = (Resolve-Path -LiteralPath $CandidateRoot).Path
$BaselineRoot = (Resolve-Path -LiteralPath $BaselineRoot).Path
$pg = @{}
foreach ($name in 'initdb', 'pg_ctl', 'psql', 'pg_dump') {
    $pg[$name] = (Get-Command (Join-Path $PostgresBin $name) -CommandType Application).Source
}

function Invoke-Checked {
    param([string]$Program, [string[]]$Arguments)

    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$([IO.Path]::GetFileName($Program)) exited with code $LASTEXITCODE."
    }
}

function Invoke-Ef {
    param([string]$Root, [string[]]$Arguments)

    Push-Location -LiteralPath $Root
    try {
        Invoke-Checked dotnet (@('ef') + $Arguments + $efOptions)
    }
    finally {
        Pop-Location
    }
}

function Invoke-TestSql {
    param([string]$Database, [string[]]$Arguments)

    $connection = "host=127.0.0.1 hostaddr=127.0.0.1 port=$port dbname=$Database user=mealtrace_rehearsal"
    Invoke-Checked $pg.psql (@('-X', '-w', '-At', '-v', 'ON_ERROR_STOP=1', '--dbname', $connection) + $Arguments)
}

function Get-MigrationHistory {
    param([string]$Database)

    Invoke-TestSql $Database @('-f', $historyQuery)
}

function Save-LegacyRows {
    param([string]$OutputPath)

    Invoke-TestSql upgraded @('-o', $OutputPath, '-f', $legacyQuery)
}

function Assert-MatchingFiles {
    param([string]$First, [string]$Second, [string]$Failure)

    if ((Get-FileHash -LiteralPath $First).Hash -ne (Get-FileHash -LiteralPath $Second).Hash) {
        throw "$Failure Compare $First and $Second."
    }
}

$output = Join-Path ([IO.Path]::GetTempPath()) "mealtrace-migrations-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $output | Out-Null
Write-Host "Migration evidence will be written to $output"
$historyQuery = Join-Path $output 'migration-history-query.sql'
$legacyQuery = Join-Path $output 'legacy-rows-query.sql'
'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' |
    Set-Content -LiteralPath $historyQuery -Encoding Ascii
@'
SELECT "Id", "Date", "MealType", "CutoffAt", "SettledAt" FROM "MealDays" ORDER BY "Id";
SELECT "Id", "MealDayId", "Count", "Version", "SupersedesId", "SettledAt", "SettledBy", "Reason"
FROM "PortionSettlements" ORDER BY "Id";
SELECT "Id", "MealDayId", "AcknowledgedBy", "AcknowledgedAt", "PortionsCount", "SnapshotToken"
FROM "SettlementAcknowledgements" ORDER BY "Id";
SELECT "Id", "MealDayId", "SettlementId", "SignedAt", "EnergyKcalPerPortion", "SourceJson"
FROM "ReportSnapshots" ORDER BY "Id";
SELECT "Id", "AgeGroup", "Nutrient", "MinValue", "Source", "EffectiveFrom", "RecordedAt"
FROM "NutrientReferenceRanges" ORDER BY "Id";
'@ | Set-Content -LiteralPath $legacyQuery -Encoding Ascii
$data = Join-Path $output 'pgdata'
$efOptions = @('--project', 'be/src/MealTrace.Infrastructure', '--startup-project', 'be/src/MealTrace.Api',
    '--configuration', 'Release', '--no-build')
$settings = @{
    DOTNET_ENVIRONMENT = 'Testing'
    ASPNETCORE_ENVIRONMENT = 'Testing'
    Jwt__Key = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([Guid]::NewGuid().ToString('N')))
    Jwt__Issuer = 'MealTrace.MigrationRehearsal'
    Jwt__Audience = 'MealTrace.MigrationRehearsal'
    PGPASSWORD = [Guid]::NewGuid().ToString('N')
    PGOPTIONS = '-c timezone=UTC'
}
$previousSettings = @{}
foreach ($name in @($settings.Keys) + 'ConnectionStrings__MealTrace') {
    $previousSettings[$name] = [Environment]::GetEnvironmentVariable($name)
}

$clusterStarted = $false
try {
    foreach ($name in $settings.Keys) {
        [Environment]::SetEnvironmentVariable($name, $settings[$name])
    }
    $passwordFile = Join-Path $output 'initdb-password'
    try {
        Set-Content -LiteralPath $passwordFile -Value $settings.PGPASSWORD -Encoding Ascii
        Invoke-Checked $pg.initdb @('-D', $data, '-U', 'mealtrace_rehearsal', '-A', 'scram-sha-256',
            "--pwfile=$passwordFile", '--encoding=UTF8', '--locale=C')
    }
    finally {
        if (Test-Path -LiteralPath $passwordFile) {
            Remove-Item -LiteralPath $passwordFile
        }
    }

    $portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $portProbe.Start()
    try {
        $port = $portProbe.LocalEndpoint.Port
    }
    finally {
        $portProbe.Stop()
    }
    Invoke-Checked $pg.pg_ctl @('-D', $data, '-l', (Join-Path $output 'postgres.log'),
        '-o', "-h 127.0.0.1 -p $port", '-w', 'start')
    $clusterStarted = $true
    $env:ConnectionStrings__MealTrace = "Host=127.0.0.1;Port=$port;Database=upgraded;Username=mealtrace_rehearsal;Pooling=false"
    Invoke-TestSql postgres @('-c', 'CREATE DATABASE upgraded', '-c', 'CREATE DATABASE fresh') | Out-Null
    Invoke-TestSql postgres @('-c', 'SHOW server_version') | Set-Content (Join-Path $output 'server-version.txt')

    foreach ($root in @($BaselineRoot, $CandidateRoot) | Select-Object -Unique) {
        Invoke-Checked dotnet @('build', (Join-Path $root 'be/MealTrace.sln'), '-c', 'Release', '-m:1')
    }

    # Run the baseline assembly first so rewritten historical migrations cannot mask an upgrade failure.
    Invoke-Ef $BaselineRoot @('database', 'update', $BaselineMigration)
    $baselineHistory = @(Get-MigrationHistory upgraded)
    if ($baselineHistory[-1] -cne $BaselineMigration) {
        throw "The upgraded database did not stop at $BaselineMigration."
    }
    $baselineHistory | Set-Content (Join-Path $output 'baseline-history.txt')
    Invoke-TestSql upgraded @('-f', (Join-Path $PSScriptRoot 'postgres-migration-fixture.sql')) | Out-Null
    $before = Join-Path $output 'legacy-before.txt'
    $after = Join-Path $output 'legacy-after.txt'
    Save-LegacyRows $before
    if (@(Get-Content -LiteralPath $before).Count -ne 6) {
        throw 'Expected six legacy fixture rows on the baseline.'
    }

    Invoke-Ef $CandidateRoot @('migrations', 'has-pending-model-changes')
    $upgradeSql = Join-Path $output 'upgrade.sql'
    Invoke-Ef $CandidateRoot @('migrations', 'script', $BaselineMigration, '--idempotent', '--output', $upgradeSql)
    Invoke-Ef $CandidateRoot @('database', 'update')
    Save-LegacyRows $after
    Assert-MatchingFiles $before $after 'The upgrade changed legacy fixture data.'

    $env:ConnectionStrings__MealTrace = "Host=127.0.0.1;Port=$port;Database=fresh;Username=mealtrace_rehearsal;Pooling=false"
    Invoke-Ef $CandidateRoot @('database', 'update')
    $dumpKey = [Guid]::NewGuid().ToString('N')
    foreach ($database in 'fresh', 'upgraded') {
        Get-MigrationHistory $database | Set-Content (Join-Path $output "$database-history.txt")
        $connection = "host=127.0.0.1 hostaddr=127.0.0.1 port=$port dbname=$database user=mealtrace_rehearsal"
        Invoke-Checked $pg.pg_dump @('--schema-only', '--no-owner', '--no-password',
            "--restrict-key=$dumpKey", '--dbname', $connection, '--file', (Join-Path $output "$database-schema.sql"))
    }
    Assert-MatchingFiles (Join-Path $output 'fresh-history.txt') (Join-Path $output 'upgraded-history.txt') 'Migration histories differ.'
    Assert-MatchingFiles (Join-Path $output 'fresh-schema.sql') (Join-Path $output 'upgraded-schema.sql') 'Schema dumps differ.'
    $finalHistory = @(Get-Content -LiteralPath (Join-Path $output 'upgraded-history.txt'))
    if ($finalHistory.Count -lt $baselineHistory.Count -or
        ($finalHistory[0..($baselineHistory.Count - 1)] -join "`n") -cne ($baselineHistory -join "`n")) {
        throw 'The candidate changed the order or IDs of baseline migrations.'
    }

    Write-Host "Verified fresh and upgrade parity through $($finalHistory[-1]) ($($baselineHistory.Count) -> $($finalHistory.Count) migrations)."
    Write-Host 'Six legacy fixture rows were preserved.'
}
finally {
    try {
        if ($clusterStarted) {
            Invoke-Checked $pg.pg_ctl @('-D', $data, '-m', 'fast', '-w', 'stop')
        }
    }
    finally {
        foreach ($name in $previousSettings.Keys) {
            [Environment]::SetEnvironmentVariable($name, $previousSettings[$name])
        }
    }
}
