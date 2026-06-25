# TEMPORARY: enables Cosmos DB diagnostic export to loganalytics-infra for RU/query investigation.
# Mirrors Infrastructure/cosmos-db-diagnostics.bicep (enableDiagnostics=true).
# TURN OFF after tuning: scripts/disable-cosmos-diagnostics.ps1

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SubscriptionId = 'a6b8f1a2-6163-41bc-aa6d-e33928939a6e',

    [string]$CosmosResourceGroup = 'AutomatedData',

    [string]$CosmosAccountName = 'cultpodcasts-db',

    [string]$LogAnalyticsResourceGroup = 'AutomatedInfra',

    [string]$LogAnalyticsWorkspaceName = 'loganalytics-infra',

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
$workspaceId = az monitor log-analytics workspace show `
    --resource-group $LogAnalyticsResourceGroup `
    --workspace-name $LogAnalyticsWorkspaceName `
    --query id -o tsv

if (-not $workspaceId) {
    throw "Log Analytics workspace '$LogAnalyticsWorkspaceName' was not found in '$LogAnalyticsResourceGroup'."
}

$logs = '[{"category":"DataPlaneRequests","enabled":true},{"category":"QueryRuntimeStatistics","enabled":true}]'
$metrics = '[{"category":"Requests","enabled":true}]'

Write-Host "Azure subscription: $account"
Write-Host "Cosmos account: $CosmosAccountName ($CosmosResourceGroup)"
Write-Host "Log Analytics: $LogAnalyticsWorkspaceName ($LogAnalyticsResourceGroup)"
Write-Host ''
Write-Host 'WARNING: This is TEMPORARY cost-investigation telemetry.'
Write-Host 'Disable with: scripts/disable-cosmos-diagnostics.ps1 after RU tuning is complete.'
Write-Host ''

if ($PSCmdlet.ShouldProcess($CosmosAccountName, 'Enable Cosmos DB diagnostic settings')) {
    az monitor diagnostic-settings create `
        --name $DiagnosticSettingsName `
        --resource $cosmosResourceId `
        --workspace $workspaceId `
        --logs $logs `
        --metrics $metrics `
        -o none

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to enable Cosmos diagnostics (exit code $LASTEXITCODE)."
    }

    Write-Host 'Cosmos diagnostics enabled.'
    az monitor diagnostic-settings show `
        --name $DiagnosticSettingsName `
        --resource $cosmosResourceId `
        -o json
}
