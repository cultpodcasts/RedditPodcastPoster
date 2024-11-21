@description('The Azure region into which the resources should be deployed.')
param location string = resourceGroup().location

@description('A unique suffix to add to resource names that need to be globally unique.')
@maxLength(13)
param resourceNameSuffix string = uniqueString(resourceGroup().id)

var appServiceAppName = 'cultpodcasts-api-${resourceNameSuffix}'
var appServicePlanName = 'cultpodcasts-api-plan'
var functionsStorageAccountName = 'storage-${resourceNameSuffix}'

var functionsStorageAccountConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${functionsStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${functionsStorageAccount.listKeys().keys[0].value}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
      name: 'Y1'
      capacity: 1
  }
}

resource appServiceApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appServiceAppName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      appSettings: [
        {
          name: 'FunctionsStorageAccountConnectionString'
          value: functionsStorageAccountConnectionString
        }
      ]
    }
  }
}

resource functionsStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: functionsStorageAccountName
  location: location
  kind: 'Storage'
  sku: {
      name: 'Standard_ZRS'
  }
}