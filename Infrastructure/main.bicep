@description('Location for resources.')
param location string = resourceGroup().location

module storage 'function-storage.bicep' = {
  name: 'storageDeployment'
  params: {
    Location: location
  }
}

module applicationInsights 'function-application-insights.bicep' = {
  name: 'applicationInsightsDeployment'
  params: {
    Location: location
  }
}

module function 'function.bicep' = {
  name: 'functionDeployment'
  params: {
    Location: location
    instrumentationKey: logAnalytics.outputs.instrumentationKey
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
  }
}
