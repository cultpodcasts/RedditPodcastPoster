@description('The name of the function app')
param name string

@description('Location for the function app.')
param location string

@description('The language worker runtime to load in the function app.')
@allowed([
  'dotnet-isolated'
  'node'
  'dotnet'
  'java'
])
param runtime string = 'dotnet'

@description('Storage-account for this Function')
param storageAccountName string

@description('Storage-account id')
param storageAccountId string

// @description('Application-Insights Instrumentation-Key for this Function')
// param instrumentationKey string

@description('Application-Insights Connection-String for this Function')
param applicationInsightsConnectionString string

@description('Suffix to use for resources')
param suffix string = uniqueString(resourceGroup().id)

@description('Enable public access')
param publicNetworkAccess bool = false

@description('App-Settings for the Function')
param appSettings object = {}

var functionAppName = '${name}-${suffix}'
var hostingPlanName = '${name}-plan-${suffix}'
var functionWorkerRuntime = runtime
var storageKey= listKeys(storageAccountId, '2022-05-01').keys[0].value

resource hostingPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
    size: 'Y1'
    family: 'Y'
    capacity: 0
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  properties: {
    reserved: true
    httpsOnly: true
    publicNetworkAccess: publicNetworkAccess?'Enabled':null
    serverFarmId: hostingPlan.id
    siteConfig: {
      functionAppScaleLimit: 1
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
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
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: functionWorkerRuntime
        }
      ]
    }
  }
}

module functionAppSetings 'app-settings.bicep' = {
  name: 'functionAppSettings'
  params: {
    currentAppSettings: list('${functionApp.id}/config/appsettings', '2024-04-01').properties
    appSettings: appSettings
    functionName: functionApp.name
  }
}
