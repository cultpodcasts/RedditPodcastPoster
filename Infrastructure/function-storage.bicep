@description('Storage Account type')
@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_RAGRS'
])
param storageAccountType string = 'Standard_LRS'

@description('Location for resource')
param location string

@description('Name for the Storage Account')
param storageName string= 'storage${uniqueString(resourceGroup().id)}'
var storageAccountName= storageName

@description('Containers') // List your container names here in the array
param container_names array = [
  'api-deployment'
  'discovery-deployment'
  'indexer-deployment'
]

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: storageName
  location: location
  sku: {
    name: storageAccountType
  }
  kind: 'Storage'
  properties: {
    supportsHttpsTrafficOnly: true
    defaultToOAuthAuthentication: true
  }
}

resource Containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = [for containerName in container_names: {
  name: containerName
  parent: storageAccount
  properties: {
    publicAccess: 'None'
  }
}]
output storageAccountName string = storageAccountName
output storageAccountId string = storageAccount.id