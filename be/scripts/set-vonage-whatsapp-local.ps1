$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '..\src\MealTrace.Api\appsettings.Development.local.json'
$key = Read-Host 'Vonage API key'
$sender = Read-Host 'Vonage WhatsApp sandbox sender (digits only)'
$tester = Read-Host 'Allow-listed Vietnam tester phone (example: 0901234567)'
if ([string]::IsNullOrWhiteSpace($key) -or $key.Contains(':')) { throw 'Invalid API key.' }
if ($sender -notmatch '^[1-9][0-9]{7,14}$') { throw 'Invalid sender.' }
if ($tester -notmatch '^(0[1-9][0-9]{8}|\+?84[1-9][0-9]{8})$') { throw 'Invalid tester.' }
$secure = Read-Host -AsSecureString 'Vonage API secret'
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $secret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($secret)) { throw 'API secret is required.' }
    $config = if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    if ($null -eq $config.PSObject.Properties['Notifications']) {
        $config | Add-Member -NotePropertyName Notifications -NotePropertyValue ([pscustomobject]@{})
    }
    $config.Notifications | Add-Member -NotePropertyName Messaging -NotePropertyValue ([pscustomobject]@{ Enabled = $true; TestNumber = $tester; PrimaryProvider = 'Vonage' }) -Force
    $config.Notifications | Add-Member -NotePropertyName Vonage -NotePropertyValue ([pscustomobject]@{ ApiKey = $key; ApiSecret = $secret; From = $sender }) -Force
    [System.IO.File]::WriteAllText($path, ($config | ConvertTo-Json -Depth 20), [System.Text.UTF8Encoding]::new($false))
    Write-Output 'Vonage WhatsApp preferred; existing Twilio config preserved as fallback. Restart BE.'
}
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
