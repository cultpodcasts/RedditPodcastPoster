# Applies YouTube API key app settings on Function apps when Infrastructure/functions.bicep is not deploying.
# Mirrors youtube + youTubeKeyUsage (+ app-specific YouTube settings) in functions.bicep.
#
# App settings are written as literal key values. The running app reads configuration only
# (YouTubeSettings via IConfiguration) — it never calls Key Vault.
#
# Full apply from Key Vault (recommended when bicep provision is offline):
#   .\scripts\apply-youtube-keys.ps1 -FromKeyVault
#
# Interim flow (new indexer keys only):
#   .\scripts\apply-youtube-keys.ps1 -ApiKey15 'YOUR_KEY' -ApiKey16 'YOUR_KEY' -ApplyNewKeysOnly
#
# Display names only (no key values); also clears stale Indexer __Reattempt keys:
#   .\scripts\apply-youtube-keys.ps1 -DisplayNamesOnly

[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'FromKeyVault')]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string[]]$FunctionApps = @('indexer-infra', 'discover-infra', 'api-infra'),

    [string]$KeyVaultName = 'cultpodcasts-deployment',

    [string]$KeyVaultResourceGroup = 'Management',

    [Parameter(ParameterSetName = 'DisplayNamesOnly')]
    [switch]$DisplayNamesOnly,

    [Parameter(ParameterSetName = 'FromKeyVault')]
    [switch]$FromKeyVault,

    [Parameter(ParameterSetName = 'ManualKeys')]
    [string]$ApiKey15,

    [Parameter(ParameterSetName = 'ManualKeys')]
    [string]$ApiKey16,

    [Parameter(ParameterSetName = 'ManualKeys')]
    [switch]$ApplyNewKeysOnly,

    [switch]$SkipRestart
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

# App slot -> Key Vault secret suffix (Youtube-ApiKey-N). Mirrors Infrastructure/functions.bicep youtube var.
$slotToKvKey = @{
    0  = 0
    1  = 1
    2  = 2
    3  = 3
    4  = 4
    5  = 5
    6  = 6
    7  = 7
    8  = 8
    9  = 9
    10 = 10
    11 = 11
    12 = 12
    13 = 15
    14 = 14
    15 = 16
    16 = 14
}

# Mirrors Infrastructure/functions.bicep youTubeKeyUsage.
$youTubeKeyUsage = @{
    0  = @{ Name = 'CultPodcasts'; Usage = 'Cli';      DisplayName = 'ApiKey-0 - Cli' }
    1  = @{ Name = 'CultPodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-01-CultPodcasts' }
    2  = @{ Name = 'CultPodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-02-CultPodcasts' }
    3  = @{ Name = 'CultPodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-03-CultPodcasts' }
    4  = @{ Name = 'CultPodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-04-CultPodcasts' }
    5  = @{ Name = 'CultPodcasts'; Usage = 'Discover'; DisplayName = 'ApiKey-5 - Discover' }
    6  = @{ Name = 'CultPodcasts'; Usage = 'Discover'; DisplayName = 'ApiKey-6 - Discover' }
    7  = @{ Name = 'CultPodcasts'; Usage = 'Bluesky'; DisplayName = 'ApiKey-7 - Bluesky' }
    8  = @{ Name = 'CultPodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-05-CultPodcasts' }
    9  = @{ Name = 'CultPodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-06-CultPodcasts' }
    10 = @{ Name = 'CultPodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-07-CultPodcasts' }
    11 = @{ Name = 'CultPodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-08-CultPodcasts' }
    12 = @{ Name = 'CultPodcasts'; Usage = 'Api';     DisplayName = 'ApiKey-12 - Api' }
    13 = @{ Name = 'cultpodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-09-CultPodcasts' }
    14 = @{ Name = 'CultPodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-10-CultPodcasts' }
    15 = @{ Name = 'cultpodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-11-CultPodcasts' }
    16 = @{ Name = 'CultPodcasts'; Usage = 'Indexer'; DisplayName = 'Indexer-Key-12-CultPodcasts' }
}

# Slots whose Usage is Indexer — Reattempt is unused (flat ring); remove stale keys on apply.
$indexerSlots = @($youTubeKeyUsage.Keys | Where-Object { $youTubeKeyUsage[$_].Usage -eq 'Indexer' } | Sort-Object)

function Get-IndexerReattemptSettingNames {
    foreach ($slot in $indexerSlots) {
        "youtube__Applications__${slot}__Reattempt"
    }
}

function Get-KeyVaultYouTubeSecrets {
    $secrets = @{}
    $requiredKvKeys = 0..16
    foreach ($kvKey in $requiredKvKeys) {
        $secretName = "Youtube-ApiKey-$kvKey"
        Write-Verbose "Reading Key Vault secret '$secretName'..."
        $value = az keyvault secret show `
            --vault-name $KeyVaultName `
            --name $secretName `
            --query value `
            -o tsv 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($value)) {
            throw "Failed to read Key Vault secret '$secretName' from vault '$KeyVaultName'."
        }
        $secrets[$kvKey] = $value
    }
    return $secrets
}

function Get-YouTubeApplicationsSettings {
    param(
        [hashtable]$KvSecrets,
        [int[]]$Slots,
        [switch]$IncludeApiKeys
    )

    $settings = [ordered]@{}
    foreach ($slot in $Slots) {
        $usage = $youTubeKeyUsage[$slot]
        if (-not $usage) {
            throw "No youTubeKeyUsage entry for slot $slot."
        }

        if ($IncludeApiKeys) {
            $kvKey = $slotToKvKey[$slot]
            if ($null -eq $kvKey) {
                throw "No KV mapping for app slot $slot."
            }
            $settings["youtube__Applications__${slot}__ApiKey"] = $KvSecrets[$kvKey]
        }

        $settings["youtube__Applications__${slot}__Name"] = $usage.Name
        $settings["youtube__Applications__${slot}__Usage"] = $usage.Usage
        $settings["youtube__Applications__${slot}__DisplayName"] = $usage.DisplayName
        # Indexer uses a flat key ring — never set Reattempt. Non-Indexer usages may define it.
        if ($usage.Usage -ne 'Indexer' -and $usage.Reattempt) {
            $settings["youtube__Applications__${slot}__Reattempt"] = $usage.Reattempt
        }
    }
    return $settings
}

function Get-AppSpecificYouTubeSettings {
    param([string]$AppName)

    $settings = [ordered]@{
        'delayedYouTubePublication__EvaluationThreshold' = '6:00:00'
    }

    switch ($AppName) {
        'indexer-infra' {
            $settings['indexer__ByPassYouTube'] = 'false'
            $settings['youtubeChannel__PreferUploadsPlaylist'] = 'true'
        }
        'api-infra' {
            $settings['indexer__ByPassYouTube'] = 'false'
            $settings['youtubeChannel__PreferUploadsPlaylist'] = 'true'
        }
        'discover-infra' {
            $settings['discover__IncludeYouTube'] = 'true'
            $settings['discover__Queries__4__DiscoverService'] = 'YouTube'
            $settings['discover__Queries__4__Term'] = 'Cult'
            $settings['discover__Queries__5__DiscoverService'] = 'YouTube'
            $settings['discover__Queries__5__Term'] = 'Cults'
        }
        default {
            throw "Unknown function app '$AppName'."
        }
    }

    return $settings
}

function Set-FunctionAppSettings {
    param(
        [string]$AppName,
        [hashtable]$Settings
    )

    if ($Settings.Count -eq 0) {
        return
    }

    $subscriptionId = az account show --query id -o tsv
    $uri = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Web/sites/$AppName/config/appsettings?api-version=2023-12-01"

    $body = @{ properties = @{} }
    foreach ($key in $Settings.Keys) {
        $body.properties[$key] = [string]$Settings[$key]
    }
    $jsonBody = $body | ConvertTo-Json -Depth 5 -Compress

    $tempFile = [System.IO.Path]::GetTempFileName()
    try {
        [System.IO.File]::WriteAllText($tempFile, $jsonBody, [System.Text.UTF8Encoding]::new($false))
        az rest --method PATCH --uri $uri --body "@$tempFile" -o none
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update app settings for '$AppName' via az rest (exit code $LASTEXITCODE)."
        }
    }
    finally {
        Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
    }
}

function Remove-FunctionAppSettings {
    param(
        [string]$AppName,
        [string[]]$SettingNames
    )

    if ($SettingNames.Count -eq 0) {
        return
    }

    az functionapp config appsettings delete `
        --resource-group $ResourceGroup `
        --name $AppName `
        --setting-names $SettingNames `
        -o none
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to delete app settings for '$AppName' (exit code $LASTEXITCODE)."
    }
}

function Show-YouTubeSettingsVerification {
    param([string]$AppName)

    $rows = az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $AppName `
        --query "[?contains(name, 'youtube__Applications') || contains(name, 'delayedYouTubePublication') || contains(name, 'youtubeChannel') || name=='indexer__ByPassYouTube' || name=='discover__IncludeYouTube' || starts_with(name, 'discover__Queries__4') || starts_with(name, 'discover__Queries__5')].{name:name,value:value}" `
        -o json | ConvertFrom-Json

    $kvRefCount = 0
    $literalApiKeyCount = 0
    $displayNameOk = $true
    $slot13Literal = $false
    $slot15Literal = $false
    $indexerReattemptRemaining = @()

    foreach ($row in $rows) {
        if ($row.name -like '*__ApiKey') {
            if ($row.value -like '@Microsoft.KeyVault*') {
                $kvRefCount++
            }
            else {
                $literalApiKeyCount++
            }
            if ($row.name -eq 'youtube__Applications__13__ApiKey' -and $row.value -notlike '@Microsoft.KeyVault*') {
                $slot13Literal = $true
            }
            if ($row.name -eq 'youtube__Applications__15__ApiKey' -and $row.value -notlike '@Microsoft.KeyVault*') {
                $slot15Literal = $true
            }
            $row.value = '***'
        }

        if ($row.name -like '*__DisplayName') {
            $slot = [int]($row.name -replace 'youtube__Applications__(\d+)__DisplayName', '$1')
            $expected = $youTubeKeyUsage[$slot].DisplayName
            if ($row.value -ne $expected) {
                $displayNameOk = $false
                $row | Add-Member -NotePropertyName expected -NotePropertyValue $expected -Force
            }
        }

        if ($row.name -like '*__Reattempt') {
            $slot = [int]($row.name -replace 'youtube__Applications__(\d+)__Reattempt', '$1')
            if ($youTubeKeyUsage[$slot].Usage -eq 'Indexer') {
                $indexerReattemptRemaining += $row.name
            }
        }
    }

    Write-Host "`n--- Verification: $AppName ---"
    $null = $rows | Sort-Object name | Format-Table -AutoSize
    $indexerReattemptOk = $indexerReattemptRemaining.Count -eq 0
    Write-Host "ApiKey literals: $literalApiKeyCount | KV refs: $kvRefCount | Slot13 literal: $slot13Literal | Slot15 literal: $slot15Literal | DisplayNames match bicep: $displayNameOk | Indexer Reattempt cleared: $indexerReattemptOk"
    if (-not $indexerReattemptOk) {
        Write-Host "Stale indexer Reattempt keys: $($indexerReattemptRemaining -join ', ')"
    }

    return [PSCustomObject]@{
        App                       = $AppName
        SettingsCount             = $rows.Count
        LiteralApiKeys            = $literalApiKeyCount
        KeyVaultRefs              = $kvRefCount
        Slot13Literal             = $slot13Literal
        Slot15Literal             = $slot15Literal
        DisplayNamesMatch         = $displayNameOk
        IndexerReattemptRemaining = $indexerReattemptRemaining
    }
}

$allSlots = 0..16
$settingsByApp = @{}

if ($FromKeyVault) {
    Write-Host "Reading YouTube API keys from Key Vault '$KeyVaultName'..."
    $kvSecrets = Get-KeyVaultYouTubeSecrets
    foreach ($app in $FunctionApps) {
        $appSettings = Get-YouTubeApplicationsSettings -KvSecrets $kvSecrets -Slots $allSlots -IncludeApiKeys
        $appSpecific = Get-AppSpecificYouTubeSettings -AppName $app
        foreach ($key in $appSpecific.Keys) {
            $appSettings[$key] = $appSpecific[$key]
        }
        $settingsByApp[$app] = $appSettings
    }
}
elseif ($DisplayNamesOnly) {
    foreach ($app in $FunctionApps) {
        $settingsByApp[$app] = Get-YouTubeApplicationsSettings -KvSecrets @{} -Slots $allSlots
    }
}
elseif ($ApplyNewKeysOnly) {
    if ([string]::IsNullOrWhiteSpace($ApiKey15) -or [string]::IsNullOrWhiteSpace($ApiKey16)) {
        throw 'Provide -ApiKey15 and -ApiKey16, or use -FromKeyVault / -DisplayNamesOnly.'
    }
    $manualSecrets = @{ 15 = $ApiKey15; 16 = $ApiKey16 }
    foreach ($app in $FunctionApps) {
        $appSettings = Get-YouTubeApplicationsSettings -KvSecrets $manualSecrets -Slots @(13, 15) -IncludeApiKeys
        $usageOnly = Get-YouTubeApplicationsSettings -KvSecrets @{} -Slots $allSlots
        foreach ($key in $usageOnly.Keys) {
            if (-not $appSettings.Contains($key)) {
                $appSettings[$key] = $usageOnly[$key]
            }
        }
        $settingsByApp[$app] = $appSettings
    }
}
else {
    throw 'Specify -FromKeyVault, -DisplayNamesOnly, or -ApplyNewKeysOnly with -ApiKey15/-ApiKey16.'
}

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Function apps: $($FunctionApps -join ', ')"

$indexerReattemptToRemove = Get-IndexerReattemptSettingNames
Write-Host "Indexer slots (Reattempt will be removed if present): $($indexerSlots -join ', ')"

$verification = @()
$anySettingsApplied = $false
foreach ($app in $FunctionApps) {
    $settings = $settingsByApp[$app]
    Write-Host "`n=== $app ($($settings.Count) settings, $($indexerReattemptToRemove.Count) indexer Reattempt keys to clear) ==="
    if ($PSCmdlet.ShouldProcess($app, 'Apply YouTube app settings')) {
        Set-FunctionAppSettings -AppName $app -Settings $settings
        Remove-FunctionAppSettings -AppName $app -SettingNames $indexerReattemptToRemove
        $anySettingsApplied = $true
        if (-not $SkipRestart) {
            Write-Host "Restarting $app..."
            az functionapp restart --resource-group $ResourceGroup --name $app -o none
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to restart '$app' (exit code $LASTEXITCODE)."
            }
        }
    }
    $verification += Show-YouTubeSettingsVerification -AppName $app
}

$verificationResults = @($verification | Where-Object { $_.App })

Write-Host "`n=== Summary ==="
foreach ($result in $verificationResults) {
    Write-Host ("{0}: {1} settings | ApiKey literals: {2} | KV refs: {3} | Slot13 literal: {4} | Slot15 literal: {5} | DisplayNames OK: {6}" -f `
        $result.App, $result.SettingsCount, $result.LiteralApiKeys, $result.KeyVaultRefs, $result.Slot13Literal, $result.Slot15Literal, $result.DisplayNamesMatch)
}

if ($verificationResults.Count -ne $FunctionApps.Count) {
    throw "Verification failed: expected $($FunctionApps.Count) app results, got $($verificationResults.Count)."
}

if ($verificationResults | Where-Object { $_.KeyVaultRefs -gt 0 -or -not $_.DisplayNamesMatch }) {
    throw 'Verification failed: KV references remain or DisplayNames do not match bicep.'
}

if ($anySettingsApplied -and ($verificationResults | Where-Object { $_.IndexerReattemptRemaining.Count -gt 0 })) {
    throw 'Verification failed: stale youtube__Applications__*__Reattempt keys remain on Indexer slots.'
}

if ($FromKeyVault -and ($verificationResults | Where-Object { -not $_.Slot13Literal -or -not $_.Slot15Literal })) {
    throw 'Verification failed: slots 13 and/or 15 ApiKey are not literal values.'
}

Write-Host "`nYouTube app settings applied successfully."
