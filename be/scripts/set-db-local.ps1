$ErrorActionPreference = 'Stop'
$path = (Join-Path $PSScriptRoot '..\src\MealTrace.Api\appsettings.Development.local.json')

Write-Output 'Enter the PostgreSQL connection string for database mealtrace. Input is hidden.'
Write-Output 'Example: Host=localhost;Port=5432;Database=mealtrace;Username=postgres;Password=...'
$secure = Read-Host -AsSecureString 'Connection string'
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $value = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'Connection string cannot be empty.' }
    $config = if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    if ($null -eq $config.PSObject.Properties['ConnectionStrings']) {
        $config | Add-Member -NotePropertyName 'ConnectionStrings' -NotePropertyValue ([pscustomobject]@{})
    }
    $config.ConnectionStrings | Add-Member -NotePropertyName 'MealTrace' -NotePropertyValue $value -Force
    $json = $config | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($path, $json, (New-Object System.Text.UTF8Encoding($false)))
    $saved = (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json).ConnectionStrings.MealTrace
    if ($saved -ne $value) { throw 'Connection string could not be verified after saving.' }
    Write-Output 'Connection string saved to an ignored local config file.'
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
}
