# Disables TEMPORARY Cosmos DB diagnostic export to loganalytics-infra.
# Mirrors Infrastructure/cosmos-db-diagnostics.bicep with enableDiagnostics=false.

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SubscriptionId = 'a6b8f1a2-6163-41bc-aa6d-e33928939a6e',

    [string]$CosmosResourceGroup = 'AutomatedData',

    [string]$CosmosAccountName = 'cultpodcasts-db',

    [string]$DiagnosticSettingsName = 'cosmos-to-loganalytics-infra'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) was not found on PATH.'
}

$account = az account show --query name -o tsv 2>$null
if (-not $account) {
    throw 'Azure CLI is not logged in. Run az login first.'
}

$cosmosResourceId = "/subscriptions/$SubscriptionId/resourceGroups/$CosmosResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosAccountName"

Write-Host "Azure subscription: $account"
Write-Host "Cosmos account: $CosmosAccountName ($CosmosResourceGroup)"
Write-Host "Removing diagnostic settings: $DiagnosticSettingsName"
Write-Host ''

if ($PSCmdlet.ShouldProcess($CosmosAccountName, 'Disable Cosmos DB diagnostic settings')) {
    az monitor diagnostic-settings delete `
        --name $DiagnosticSettingsName `
        --resource $cosmosResourceId `
        -o none

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to disable Cosmos diagnostics (exit code $LASTEXITCODE)."
    }

    Write-Host 'Cosmos diagnostics disabled.'
}
