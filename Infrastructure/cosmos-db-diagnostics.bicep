// TEMPORARY cost-investigation export: Cosmos DB -> loganalytics-infra.
// After RU tuning is complete, disable via:
//   scripts/disable-cosmos-diagnostics.ps1
// or redeploy with enableDiagnostics=false.

@description('Cosmos DB account name')
param cosmosAccountName string

@description('Resource group containing the Cosmos DB account')
param cosmosDatabaseResourceGroupName string = 'AutomatedData'

@description('Log Analytics workspace resource group')
param logAnalyticsResourceGroupName string = 'AutomatedInfra'

@description('Log Analytics workspace name')
param logAnalyticsWorkspaceName string = 'loganalytics-infra'

@description('TEMPORARY: export Cosmos DB data-plane and query-runtime logs to Log Analytics for RU attribution. Set false after investigation.')
param enableDiagnostics bool = true

resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: cosmosAccountName
  scope: resourceGroup(cosmosDatabaseResourceGroupName)
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
