@description('Location for resources.')
param location string = resourceGroup().location

module storage 'function-storage.bicep' = {
  name: 'storageDeployment'
  params: {
    storageAccountName: storageAccountName
    storageAccountId: storageAccountId
  }
}

module applicationInsights 'function-application-insights.bicep' = {
  name: 'applicationInsightsDeployment'
  params: {
    instrumentationKey: instrumentationKey
  }
}

module function 'function.bicep' = {
  name: 'functionDeployment'
  params: {
    instrumentationKey: logAnalytics.outputs.instrumentationKey
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
  }
}
