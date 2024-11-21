@description('Location for resources.')
param location string = resourceGroup().location

@description('Suffix to use for resources')
param suffix string = uniqueString(resourceGroup().id)

module storage 'function-storage.bicep' = {
  name: 'storageDeployment'
  params: {
    location: location
    suffix: suffix
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
    name: 'Function 1'
    location: location
    suffix: suffix
    instrumentationKey: applicationInsights.outputs.instrumentationKey
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
  }
}

module function2 'function.bicep' = {
  name: 'function2Deployment'
  params: {
    name: 'Function 2'
    location: location
    suffix: suffix
    instrumentationKey: applicationInsights.outputs.instrumentationKey
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
  }
}
