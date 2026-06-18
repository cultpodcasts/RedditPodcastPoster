# Applies Indexer YouTube application DisplayName app settings on Function apps.
# Use when Infrastructure/functions.bicep is not deploying.
# Mirrors youTubeKeyUsage Indexer DisplayName entries in functions.bicep.

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string[]]$FunctionApps = @('indexer-infra', 'discover-infra', 'api-infra')
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

# Keep in sync with Infrastructure/functions.bicep youTubeKeyUsage (Indexer DisplayName only).
# HourPrimary-N = UTC hour-window primary (N maps to hours (N-1)*6..N*6-1); all keys share one indexer ring.
$settings = @(
    'youtube__Applications__1__DisplayName=Indexer-HourPrimary-1-CultPodcasts'
    'youtube__Applications__2__DisplayName=Indexer-HourPrimary-2-CultPodcasts'
    'youtube__Applications__3__DisplayName=Indexer-HourPrimary-3-CultPodcasts'
    'youtube__Applications__4__DisplayName=Indexer-HourPrimary-4-CultPodcasts'
    'youtube__Applications__8__DisplayName=Indexer-HourPrimary-1-Reattempt1-CultPodcasts'
    'youtube__Applications__9__DisplayName=Indexer-HourPrimary-2-Reattempt1-CultPodcasts'
    'youtube__Applications__10__DisplayName=Indexer-HourPrimary-3-Reattempt1-CultPodcasts'
    'youtube__Applications__11__DisplayName=Indexer-HourPrimary-4-Reattempt1-CultPodcasts'
    'youtube__Applications__13__DisplayName=Indexer-HourPrimary-1-Reattempt2-cultcodcasts'
    'youtube__Applications__14__DisplayName=Indexer-HourPrimary-2-Reattempt2-CultPodcasts'
    'youtube__Applications__15__DisplayName=Indexer-HourPrimary-3-Reattempt2-cultcodcasts'
    'youtube__Applications__16__DisplayName=Indexer-HourPrimary-4-Reattempt2-CultPodcasts'
)

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Applying $($settings.Count) Indexer YouTube DisplayName settings to each function app..."

foreach ($app in $FunctionApps) {
    Write-Host "`n=== $app ==="
    if ($PSCmdlet.ShouldProcess($app, 'Apply Indexer YouTube DisplayName app settings')) {
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
        --query "[?contains(name, 'youtube__Applications') && contains(name, 'DisplayName')].{name:name,value:value}" `
        -o table
}

Write-Host "`nIndexer YouTube DisplayName settings applied. Each app will restart to pick up changes."
