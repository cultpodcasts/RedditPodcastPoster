# Applies YouTube API key app settings on Function apps when Infrastructure/functions.bicep is not deploying.
# Mirrors youtube + youTubeKeyUsage in functions.bicep.
#
# Typical interim flow (new Reattempt2 keys only):
#   1. az keyvault secret set ... Youtube-ApiKey-15 / Youtube-ApiKey-16  (see docs/youtube-keys.md)
#   2. .\scripts\apply-youtube-keys.ps1 -FromKeyVault -ApplyNewKeysOnly
#
# Full key ring from Key Vault (all slots 0-16):
#   .\scripts\apply-youtube-keys.ps1 -FromKeyVault
#
# Display names only (no key values):
#   .\scripts\apply-youtube-keys.ps1 -DisplayNamesOnly

[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'ManualKeys')]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string]$KeyVaultName = 'cultpodcasts-deployment',

    [string[]]$FunctionApps = @('indexer-infra', 'discover-infra', 'api-infra'),

    [Parameter(ParameterSetName = 'DisplayNamesOnly')]
    [switch]$DisplayNamesOnly,

    [Parameter(ParameterSetName = 'FromKeyVault')]
    [switch]$FromKeyVault,

    [Parameter(ParameterSetName = 'ManualKeys')]
    [string]$ApiKey15,

    [Parameter(ParameterSetName = 'ManualKeys')]
    [string]$ApiKey16,

    [Parameter(ParameterSetName = 'ManualKeys')]
    [Parameter(ParameterSetName = 'FromKeyVault')]
    [switch]$ApplyNewKeysOnly
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

# Keep in sync with Infrastructure/functions.bicep youTubeKeyUsage (Indexer DisplayName entries).
$indexerDisplayNames = @(
    'youtube__Applications__1__DisplayName=Indexer-HourPrimary-1-CultPodcasts'
    'youtube__Applications__2__DisplayName=Indexer-HourPrimary-2-CultPodcasts'
    'youtube__Applications__3__DisplayName=Indexer-HourPrimary-3-CultPodcasts'
    'youtube__Applications__4__DisplayName=Indexer-HourPrimary-4-CultPodcasts'
    'youtube__Applications__8__DisplayName=Indexer-HourPrimary-1-Reattempt1-CultPodcasts'
    'youtube__Applications__9__DisplayName=Indexer-HourPrimary-2-Reattempt1-CultPodcasts'
    'youtube__Applications__10__DisplayName=Indexer-HourPrimary-3-Reattempt1-CultPodcasts'
    'youtube__Applications__11__DisplayName=Indexer-HourPrimary-4-Reattempt1-CultPodcasts'
    'youtube__Applications__13__DisplayName=Indexer-HourPrimary-1-Reattempt2-CultPodcasts'
    'youtube__Applications__13__Name=CultPodcasts'
    'youtube__Applications__14__DisplayName=Indexer-HourPrimary-2-Reattempt2-CultPodcasts'
    'youtube__Applications__15__DisplayName=Indexer-HourPrimary-3-Reattempt2-CultPodcasts'
    'youtube__Applications__15__Name=CultPodcasts'
    'youtube__Applications__16__DisplayName=Indexer-HourPrimary-4-Reattempt2-CultPodcasts'
)

function Get-KeyVaultSecret {
    param([string]$SecretName)
    az keyvault secret show --vault-name $KeyVaultName --name $SecretName --query value -o tsv
}

function Get-YouTubeKeySettingsFromKeyVault {
    $settings = @()
    for ($i = 0; $i -le 16; $i++) {
        if ($i -eq 13) {
            continue
        }

        $secretName = "Youtube-ApiKey-$i"
        $value = Get-KeyVaultSecret -SecretName $secretName
        if (-not $value) {
            if ($i -ge 15) {
                throw "Key Vault secret '$secretName' is missing. Create it first (see docs/youtube-keys.md)."
            }
            throw "Key Vault secret '$secretName' is missing."
        }

        $appIndex = switch ($i) {
            15 { 13 }
            16 { 15 }
            default { $i }
        }
        $settings += "youtube__Applications__${appIndex}__ApiKey=$value"
    }
    return $settings
}

function Get-NewYouTubeKeySettings {
    param([string]$Key15, [string]$Key16)
    if ([string]::IsNullOrWhiteSpace($Key15) -or [string]::IsNullOrWhiteSpace($Key16)) {
        throw 'Provide -ApiKey15 and -ApiKey16, or use -FromKeyVault / -DisplayNamesOnly.'
    }
    return @(
        "youtube__Applications__13__ApiKey=$Key15"
        "youtube__Applications__15__ApiKey=$Key16"
    )
}

$settings = @()
if ($DisplayNamesOnly) {
    $settings = $indexerDisplayNames
}
elseif ($FromKeyVault -and $ApplyNewKeysOnly) {
    $key15 = Get-KeyVaultSecret -SecretName 'Youtube-ApiKey-15'
    $key16 = Get-KeyVaultSecret -SecretName 'Youtube-ApiKey-16'
    if (-not $key15 -or -not $key16) {
        throw "Key Vault secrets 'Youtube-ApiKey-15' and/or 'Youtube-ApiKey-16' are missing. Create them first (see docs/youtube-keys.md)."
    }
    $settings = @(Get-NewYouTubeKeySettings -Key15 $key15 -Key16 $key16) + $indexerDisplayNames
}
elseif ($FromKeyVault) {
    $settings = @(Get-YouTubeKeySettingsFromKeyVault) + $indexerDisplayNames
}
elseif ($ApplyNewKeysOnly) {
    $settings = @(Get-NewYouTubeKeySettings -Key15 $ApiKey15 -Key16 $ApiKey16) + $indexerDisplayNames
}
else {
    $settings = @(Get-NewYouTubeKeySettings -Key15 $ApiKey15 -Key16 $ApiKey16) + $indexerDisplayNames
}

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Key Vault: $KeyVaultName"
Write-Host "Applying $($settings.Count) YouTube app settings to each function app..."

foreach ($app in $FunctionApps) {
    Write-Host "`n=== $app ==="
    if ($PSCmdlet.ShouldProcess($app, 'Apply YouTube app settings')) {
        az functionapp config appsettings set `
            --resource-group $ResourceGroup `
            --name $app `
            --settings $settings `
            -o none
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update app settings for '$app' (exit code $LASTEXITCODE)."
        }
    }

    az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $app `
        --query "[?contains(name, 'youtube__Applications') && (contains(name, 'DisplayName') || contains(name, 'ApiKey') || contains(name, '__Name'))].{name:name,value:value}" `
        -o json | ForEach-Object {
            ($_ | ConvertFrom-Json) | ForEach-Object {
                if ($_.name -like '*__ApiKey') { $_.value = '***' }
                [PSCustomObject]$_
            }
        } | Format-Table -AutoSize
}

Write-Host "`nYouTube app settings applied. Each app will restart to pick up changes."
