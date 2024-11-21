@description('Location for resources.')
param location string = resourceGroup().location

module storage 'function-storage.bicep' = {
  name: 'storageDeployment'
  params: {
    location: location
  }
}

module applicationInsights 'function-application-insights.bicep' = {
  name: 'applicationInsightsDeployment'
  params: {
    location: location
  }
}

module function 'function.bicep' = {
  name: 'functionDeployment'
  params: {
    location: location
    instrumentationKey: applicationInsights.outputs.instrumentationKey
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
  }
}
