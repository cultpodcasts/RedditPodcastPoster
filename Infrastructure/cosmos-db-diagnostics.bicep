// TEMPORARY cost-investigation export: Cosmos DB -> loganalytics-infra.
// After RU tuning is complete, disable via:
//   scripts/disable-cosmos-diagnostics.ps1
// or redeploy with enableDiagnostics=false.
//
// Deploy this file at the Cosmos account resource-group scope (AutomatedData).
// Do not set an explicit resourceGroup() scope on the Cosmos existing resource —
// diagnosticSettings scoped to a cross-RG existing parent fail BCP139 (Bicep 0.48+).

@description('Cosmos DB account name')
param cosmosAccountName string

@description('Log Analytics workspace resource group')
param logAnalyticsResourceGroupName string = 'AutomatedInfra'

@description('Log Analytics workspace name')
param logAnalyticsWorkspaceName string = 'loganalytics-infra'

@description('TEMPORARY: export Cosmos DB data-plane and query-runtime logs to Log Analytics for RU attribution. Set false after investigation.')
param enableDiagnostics bool = true

// Same RG as this deployment (DATABASE_RESOURCEGROUP_NAME / AutomatedData).
resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: cosmosAccountName
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: logAnalyticsWorkspaceName
  scope: resourceGroup(logAnalyticsResourceGroupName)
}

resource cosmosDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (enableDiagnostics) {
  name: 'cosmos-to-loganalytics-infra'
  scope: cosmosDbAccount
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      {
        category: 'DataPlaneRequests'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'QueryRuntimeStatistics'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
    metrics: [
      {
        category: 'Requests'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
  }
}

output diagnosticsEnabled bool = enableDiagnostics
output diagnosticSettingsName string = enableDiagnostics ? cosmosDiagnostics.name : ''
