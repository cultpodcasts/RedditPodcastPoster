@description('Location for resources.')
param location string = resourceGroup().location

@description('Suffix to use for resources')
param suffix string = uniqueString(resourceGroup().id)

@description('Name for the Storage Account')
param storageName string= 'storage${uniqueString(resourceGroup().id)}'

@description('Runtime for the Functions')
@allowed([
  'dotnet-isolated'
  'node'
  'dotnet'
  'java'
])
param runtime string

module storage 'function-storage.bicep' = {
  name: 'storageDeployment'
  params: {
    location: location
    storageName: storageName
  }
}

module applicationInsights 'function-application-insights.bicep' = {
  name: 'applicationInsightsDeployment'
  params: {
    location: location
    suffix: suffix
  }
}

module apiFunction 'function.bicep' = {
  name: 'apiFunctionDeployment'
  params: {
    name: 'api'
    location: location
    instrumentationKey: applicationInsights.outputs.instrumentationKey
    applicationInsightsConnectionString: applicationInsights.outputs.connectionString
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
    runtime: runtime
    suffix: suffix
    publicNetworkAccess: true
  }
}

module discoveryFunction 'function.bicep' = {
  name: 'discoveryFunctionDeployment'
  params: {
    name: 'discover'
    location: location
    instrumentationKey: applicationInsights.outputs.instrumentationKey
    applicationInsightsConnectionString: applicationInsights.outputs.connectionString
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
    runtime: runtime
    suffix: suffix
    publicNetworkAccess: false
  }
}

module indexerFunction 'function.bicep' = {
  name: 'indexerFunctionDeployment'
  params: {
    name: 'indexer'
    location: location
    instrumentationKey: applicationInsights.outputs.instrumentationKey
    applicationInsightsConnectionString: applicationInsights.outputs.connectionString
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
    runtime: runtime
    suffix: suffix
    publicNetworkAccess: false
  }
}

