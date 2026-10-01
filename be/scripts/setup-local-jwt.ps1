$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '..\src\MealTrace.Api\appsettings.Development.local.json'
$config = if (Test-Path -LiteralPath $path) {
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
} else { [pscustomobject]@{} }

if ($null -eq $config.PSObject.Properties['Jwt']) {
    $config | Add-Member -NotePropertyName 'Jwt' -NotePropertyValue ([pscustomobject]@{})
}
if ([string]::IsNullOrWhiteSpace($config.Jwt.Key)) {
    $bytes = New-Object byte[] 64
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    $config.Jwt | Add-Member -NotePropertyName 'Key' -NotePropertyValue ([Convert]::ToBase64String($bytes)) -Force
    [IO.File]::WriteAllText($path, ($config | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
}

try { $keyBytes = [Convert]::FromBase64String($config.Jwt.Key) }
catch { throw 'Existing Jwt:Key is not valid Base64. Check the local configuration.' }
if ($keyBytes.Length -lt 32) { throw 'Existing Jwt:Key must contain at least 32 bytes.' }
Write-Output 'JWT key is configured in the ignored local config file. Existing settings are preserved.'
