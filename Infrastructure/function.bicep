@description('The name of the function app')
param name string

@description('Location for the function app.')
param location string

@description('The language worker runtime to load in the function app.')
@allowed([
  'node'
  'dotnet'
  'java'
])
param runtime string = 'dotnet'

@description('Storage-account for this Function')
param storageAccountName string

@description('Storage-account id')
param storageAccountId string

@description('Application-Insights Instrumentation-Key for this Function')
param instrumentationKey string

@description('Suffix to use for resources')
param suffix string = uniqueString(resourceGroup().id)

var functionAppName = '${name}-${suffix}'
var hostingPlanName = '${name}-plan-${suffix}'
var functionWorkerRuntime = runtime
var storageKey= listKeys(storageAccountId, '2022-05-01').keys[0].value

resource hostingPlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

resource functionApp 'Microsoft.Web/sites@2021-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageKey}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageKey}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'WEBSITE_NODE_DEFAULT_VERSION'
          value: '~14'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: instrumentationKey
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: functionWorkerRuntime
        }
      ]
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      functionAppScaleLimit: 1
    }
    httpsOnly: true
    siteProperties: {
        properties: [
            {
                "name": "LinuxFxVersion"
                "value": "DOTNET-ISOLATED|8.0"
            }
        ]
    }
    kind: 'functionapp,linux'
    
  }
}