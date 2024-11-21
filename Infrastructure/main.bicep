@description('Location for resources.')
param location string = resourceGroup().location

@description('Suffix to use for resources')
param suffix string = uniqueString(resourceGroup().id)

@description('Name for the Storage Account')
param storageName string= 'storage${uniqueString(resourceGroup().id)}'

@description('Runtime for the Functions')
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

module function1 'function.bicep' = {
  name: 'function1Deployment'
  params: {
    name: 'function1'
    location: location
    instrumentationKey: applicationInsights.outputs.instrumentationKey
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
    runtime: runtime
  }
}

module function2 'function.bicep' = {
  name: 'function2Deployment'
  params: {
    name: 'function2'
    location: location
    instrumentationKey: applicationInsights.outputs.instrumentationKey
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
    runtime: runtime
  }
}

