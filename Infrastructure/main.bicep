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

@description('App-Settings for Api-Function')
param apiSettings object = {}

@description('App-Settings for Discover-Function')
param discoverySettings object = {}

@description('App-Settings for Indexer-Function')
param indexerSettings object = {}

@secure()
param auth0ClientId string

@secure()
param auth0ClientSecret string

@secure()
param blueskyPassword string

@secure()
param cloudflareAccountId string

@secure()
param cloudflareKVApiToken string

@secure()
param cloudflareListsApiToken string

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
    // instrumentationKey: applicationInsights.outputs.instrumentationKey
    applicationInsightsConnectionString: applicationInsights.outputs.connectionString
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
    runtime: runtime
    suffix: suffix
    publicNetworkAccess: true
    appSettings: union({
        auth0client__ClientId: auth0ClientId
        auth0client__ClientSecret: auth0ClientSecret
        bluesky__Password: blueskyPassword
        cloudflare__AccountId: cloudflareAccountId
        cloudflare__KVApiToken: cloudflareKVApiToken
        cloudflare__ListsApiToken: cloudflareListsApiToken
    }, apiSettings)
  }
}

module discoveryFunction 'function.bicep' = {
  name: 'discoveryFunctionDeployment'
  params: {
    name: 'discover'
    location: location
    // instrumentationKey: applicationInsights.outputs.instrumentationKey
    applicationInsightsConnectionString: applicationInsights.outputs.connectionString
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
    runtime: runtime
    suffix: suffix
    publicNetworkAccess: false
    appSettings: discoverySettings
  }
}

module indexerFunction 'function.bicep' = {
  name: 'indexerFunctionDeployment'
  params: {
    name: 'indexer'
    location: location
    // instrumentationKey: applicationInsights.outputs.instrumentationKey
    applicationInsightsConnectionString: applicationInsights.outputs.connectionString
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
    runtime: runtime
    suffix: suffix
    publicNetworkAccess: false
    appSettings: indexerSettings
  }
}

