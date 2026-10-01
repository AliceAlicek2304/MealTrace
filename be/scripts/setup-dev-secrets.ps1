param([switch]$ShowPasswords)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\MealTrace.Api\MealTrace.Api.csproj'
$project = (Resolve-Path -LiteralPath $project).Path
$id = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.UserSecretsId
$secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$id\secrets.json"

function Read-Secrets {
    if (Test-Path -LiteralPath $secretsPath) {
        return Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
    }
    return [pscustomobject]@{}
}

function Get-Secret($secrets, [string]$key) {
    $property = $secrets.PSObject.Properties[$key]
    if ($null -ne $property) { return [string]$property.Value }
    return $null
}

function Save-Secret([string]$key, [string]$value) {
    & dotnet user-secrets set $key $value --project $project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not save secret $key" }
}

$secrets = Read-Secrets
if (-not (Get-Secret $secrets 'Jwt:Key')) {
    $bytes = New-Object byte[] 64
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    Save-Secret 'Jwt:Key' ([Convert]::ToBase64String($bytes))
}

$roles = @('ADMIN', 'TEACHER', 'KITCHEN_STAFF', 'PARENT')
foreach ($role in $roles) {
    $secrets = Read-Secrets
    $key = "Seed:Passwords:$role"
    if (-not (Get-Secret $secrets $key)) {
        Save-Secret $key '123456@@'
    }
}

Write-Output 'JWT key and four Development seed passwords are stored in .NET User Secrets outside the repository.'
if ($ShowPasswords) {
    $secrets = Read-Secrets
    foreach ($role in $roles) {
        $email = switch ($role) {
            'ADMIN' { 'admin@demo.mealtrace.local' }
            'TEACHER' { 'teacher@demo.mealtrace.local' }
            'KITCHEN_STAFF' { 'kitchen@demo.mealtrace.local' }
            'NUTRITIONIST' { 'nutrition@demo.mealtrace.local' }
            'ACCOUNTANT' { 'accountant@demo.mealtrace.local' }
            'PARENT' { 'parent@demo.mealtrace.local' }
        }
        Write-Output "$role  $email  $(Get-Secret $secrets "Seed:Passwords:$role")"
    }
}
