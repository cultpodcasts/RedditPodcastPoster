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

@description('Target language version used by the function app.')
@allowed([ '8.0', '9.0', '10.0'])
param runtimeVersion string = '10.0' 

@description('Storage-container-blob-endpoint for this Function')
param storageUrl string

@description('Application-Insights Connection-String for this Function')
param applicationInsightsConnectionString string

@description('Suffix to use for resources')
param suffix string = uniqueString(resourceGroup().id)

@description('Enable public access')
param publicNetworkAccess bool = false

@description('App-Settings for the Function')
param appSettings object = {}

@description('The memory size of instances used by the app.')
@allowed([2048,4096])
param instanceMemoryMB int = 2048

var functionAppName = '${name}-${suffix}'
var hostingPlanName = '${name}-plan-${suffix}'
var functionWorkerRuntime = runtime

resource hostingPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
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
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: storageUrl
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 40
        instanceMemoryMB: instanceMemoryMB
      }
      runtime: { 
        name: runtime
        version: runtimeVersion
      }
    }
  }
}

module functionAppSetings 'app-settings.bicep' = {
  name: '${deployment().name}-appsettings'
  params: {
    currentAppSettings: list('${functionApp.id}/config/appsettings', '2020-12-01').properties
    appSettings: union({
        APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsightsConnectionString
        WEBSITE_CONTENTSHARE: toLower(functionAppName)
        FUNCTIONS_EXTENSION_VERSION: '~4'
        FUNCTIONS_WORKER_RUNTIME: functionWorkerRuntime
        WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED: '1'
    }, appSettings)
    functionName: functionApp.name
  }
}
