# Applies discovery ML scorer app settings on discover-infra.
# Use when Infrastructure/functions.bicep is not deploying.
# Mirrors discoverScorer in functions.bicep (discover function only).

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ResourceGroup = 'AutomatedInfra',

    [string]$FunctionApp = 'discover-infra',

    [string]$StorageAccount = 'cultpodcastsstg',

    [string]$BlobContainerName = 'discovery-models',

    [string]$BlobPrefix = 'current',

    [string]$AutoHideThreshold = '0.05',

    [ValidateSet('true', 'false')]
    [string]$Enabled = 'true'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

# Keep in sync with Infrastructure/functions.bicep discoverScorer.
$settings = @(
    "discover__scorer__Enabled=$Enabled"
    "discover__scorer__BlobStorageAccountName=$StorageAccount"
    "discover__scorer__BlobContainerName=$BlobContainerName"
    "discover__scorer__BlobPrefix=$BlobPrefix"
    "discover__scorer__AutoHideThreshold=$AutoHideThreshold"
)

Write-Host "Azure subscription: $account"
Write-Host "Resource group: $ResourceGroup"
Write-Host "Function app: $FunctionApp"
Write-Host "Applying discovery scorer app settings..."

if ($PSCmdlet.ShouldProcess($FunctionApp, 'Apply discovery scorer app settings')) {
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
    --query "[?starts_with(name, 'discover__scorer__')].{name:name,value:value}" `
    -o table

Write-Host "`nDiscovery scorer settings applied. Function app will restart to pick up changes."
