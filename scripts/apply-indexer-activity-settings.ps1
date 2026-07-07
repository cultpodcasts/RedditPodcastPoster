# Applies indexer activity toggles and trigger disables directly on indexer-infra.
# Use when Infrastructure/functions.bicep is not deploying via GitHub Actions.
# Mirrors indexerActivities + indexerTriggers in Infrastructure/functions.bicep.

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string]$FunctionApp = 'indexer-infra',

    [ValidateSet('true', 'false')]
    [string]$RunIndex = 'true',

    [ValidateSet('true', 'false')]
    [string]$RunCategoriser = 'true',

    [ValidateSet('true', 'false')]
    [string]$RunPoster = 'false',

    [ValidateSet('true', 'false')]
    [string]$RunPublisher = 'true',

    [ValidateSet('true', 'false')]
    [string]$RunTweet = 'false',

    [ValidateSet('true', 'false')]
    [string]$RunBluesky = 'true',

    [ValidateSet('true', 'false')]
    [string]$HalfHourlyDisabled = 'true'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

# Keep in sync with Infrastructure/functions.bicep (indexerActivities + indexerTriggers).
$settings = @(
    "activities__RunIndex=$RunIndex"
    "activities__RunCategoriser=$RunCategoriser"
    "activities__RunPoster=$RunPoster"
    "activities__RunPublisher=$RunPublisher"
    "activities__RunTweet=$RunTweet"
    "activities__RunBluesky=$RunBluesky"
    "AzureWebJobs_HalfHourly_Disabled=$HalfHourlyDisabled"
)

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Function app: $FunctionApp"
Write-Host "Applying indexer activity and trigger settings..."

if ($PSCmdlet.ShouldProcess($FunctionApp, 'Apply indexer activity settings')) {
    az functionapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $FunctionApp `
        --settings $settings `
        -o none

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to update app settings for '$FunctionApp' (exit code $LASTEXITCODE)."
    }
}

az functionapp config appsettings list `
    --resource-group $ResourceGroup `
    --name $FunctionApp `
    --query "[?starts_with(name, 'activities__Run') || name=='AzureWebJobs_HalfHourly_Disabled'].{name:name,value:value}" `
    -o table

Write-Host "`nIndexer activity settings applied. Function app will restart to pick up changes."
