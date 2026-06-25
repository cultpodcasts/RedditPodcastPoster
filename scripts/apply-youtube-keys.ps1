# Applies YouTube API key app settings on Function apps when Infrastructure/functions.bicep is not deploying.
# Mirrors youtube + youTubeKeyUsage in functions.bicep.
#
# App settings are written as literal key values. The running app reads configuration only
# (YouTubeSettings via IConfiguration) — it never calls Key Vault.
#
# Typical interim flow (new Reattempt2 keys only):
#   .\scripts\apply-youtube-keys.ps1 -ApiKey15 'YOUR_KEY' -ApiKey16 'YOUR_KEY' -ApplyNewKeysOnly
#
# Display names only (no key values):
#   .\scripts\apply-youtube-keys.ps1 -DisplayNamesOnly

[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'ManualKeys')]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string[]]$FunctionApps = @('indexer-infra', 'discover-infra', 'api-infra'),

    [Parameter(ParameterSetName = 'DisplayNamesOnly')]
    [switch]$DisplayNamesOnly,

    [Parameter(ParameterSetName = 'ManualKeys')]
    [string]$ApiKey15,

    [Parameter(ParameterSetName = 'ManualKeys')]
    [string]$ApiKey16,

    [Parameter(ParameterSetName = 'ManualKeys')]
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
    'youtube__Applications__13__Name=cultpodcasts'
    'youtube__Applications__14__DisplayName=Indexer-HourPrimary-2-Reattempt2-CultPodcasts'
    'youtube__Applications__15__DisplayName=Indexer-HourPrimary-3-Reattempt2-CultPodcasts'
    'youtube__Applications__15__Name=cultpodcasts'
    'youtube__Applications__16__DisplayName=Indexer-HourPrimary-4-Reattempt2-CultPodcasts'
)

function Get-NewYouTubeKeySettings {
    param([string]$Key15, [string]$Key16)
    if ([string]::IsNullOrWhiteSpace($Key15) -or [string]::IsNullOrWhiteSpace($Key16)) {
        throw 'Provide -ApiKey15 and -ApiKey16, or use -DisplayNamesOnly.'
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
elseif ($ApplyNewKeysOnly) {
    $settings = @(Get-NewYouTubeKeySettings -Key15 $ApiKey15 -Key16 $ApiKey16) + $indexerDisplayNames
}
else {
    $settings = @(Get-NewYouTubeKeySettings -Key15 $ApiKey15 -Key16 $ApiKey16) + $indexerDisplayNames
}

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
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
