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

@description('Kind for an account other than cultpodcastsstg. New accounts reject Storage (AccountKindNotSupported) and must use StorageV2. The live production account stays Storage: upgrading it is one-way and this module will not do it.')
@allowed([
  'Storage'
  'StorageV2'
])
param storageKind string = 'Storage'

var storageAccountName = storageName
// Signpost: main keeps the existing kind even if a caller passes StorageV2 for this name.
var productionStorageAccountName = 'cultpodcastsstg'
var accountKind = storageName == productionStorageAccountName ? 'Storage' : storageKind

@description('Containers') // List your container names here in the array
param container_names array = [
  'api-deployment'
  'discovery-deployment'
  'indexer-deployment'
  'discovery-models'
]

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: storageName
  location: location
  sku: {
    name: storageAccountType
  }
  kind: accountKind
  properties: {
    supportsHttpsTrafficOnly: true
    defaultToOAuthAuthentication: true
  }
}

resource Blob_Services 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource Containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = [for containerName in container_names: {
  name: containerName
  parent: Blob_Services
  properties: {
    publicAccess: 'None'
  }
}]
output storageAccountName string = storageAccountName
output storageAccountId string = storageAccount.id