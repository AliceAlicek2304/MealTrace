$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\src\MealTrace.Api\MealTrace.Api.csproj')).Path
$id = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.UserSecretsId
$folder = Join-Path $env:APPDATA "Microsoft\UserSecrets\$id"
$path = Join-Path $folder 'secrets.json'

Write-Output 'Enter the PostgreSQL connection string for database mealtrace. Input is hidden.'
Write-Output 'Example: Host=localhost;Port=5432;Database=mealtrace;Username=postgres;Password=...'
$secure = Read-Host -AsSecureString 'Connection string'
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $value = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'Connection string cannot be empty.' }
    if (-not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Path $folder | Out-Null }
    $secrets = if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    $secrets | Add-Member -NotePropertyName 'ConnectionStrings:MealTrace' -NotePropertyValue $value -Force
    $secrets | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding utf8
    $saved = (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json).PSObject.Properties['ConnectionStrings:MealTrace'].Value
    if ($saved -ne $value) { throw 'Connection string could not be verified after saving.' }
    Write-Output 'Connection string saved to .NET User Secrets outside the repository.'
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
}
