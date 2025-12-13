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

resource symbolicname 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-06-01' = {
  parent: storageName
  name: 'indexer-deployment'
}

output storageAccountName string = storageAccountName
output storageAccountId string = storageAccount.id