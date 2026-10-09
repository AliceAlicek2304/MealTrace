$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '..\src\MealTrace.Api\appsettings.Development.local.json'
$sid = Read-Host 'Twilio Account SID (AC...)'
$senderNumber = Read-Host 'WhatsApp trial sender in E.164 format (+...)'
$template = Read-Host 'Approved trial Content SID (HX...)'
$tester = Read-Host 'Verified tester phone (example: 0901234567)'
if ($sid -notmatch '^AC[0-9a-fA-F]{32}$') { throw 'Invalid Account SID.' }
if ($template -notmatch '^HX[0-9a-fA-F]{32}$') { throw 'Invalid Content SID.' }
if ($senderNumber -notmatch '^\+[1-9][0-9]{7,14}$') { throw 'Invalid sender.' }
if ($tester -notmatch '^(0[1-9][0-9]{8}|\+?84[1-9][0-9]{8})$') { throw 'Invalid Vietnam tester number.' }
$secure = Read-Host -AsSecureString 'Twilio Auth Token'
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $token = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'Auth Token is required.' }
    $config = if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    if ($null -eq $config.PSObject.Properties['Notifications']) {
        $config | Add-Member -NotePropertyName Notifications -NotePropertyValue ([pscustomobject]@{})
    }
    if ($null -eq $config.Notifications.PSObject.Properties['Messaging']) {
        $config.Notifications | Add-Member -NotePropertyName Messaging -NotePropertyValue ([pscustomobject]@{ Enabled = $true; TestNumber = $tester; PrimaryProvider = 'Twilio' })
    }
    $config.Notifications | Add-Member -NotePropertyName Twilio -NotePropertyValue ([pscustomobject]@{ AccountSid = $sid; AuthToken = $token; From = $senderNumber; ContentSid = $template }) -Force
    [System.IO.File]::WriteAllText($path, ($config | ConvertTo-Json -Depth 20), [System.Text.UTF8Encoding]::new($false))
    Write-Output 'Twilio WhatsApp configured in ignored local file. Restart BE. Tester must join the trial first.'
}
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
