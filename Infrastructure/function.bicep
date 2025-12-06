@description('The name of the function app')
param name string

@description('Location for the function app.')
param location string

@description('The language worker runtime to load in the function app.')
@allowed([
  'dotnet-isolated'
  'node'
  'dotnet'
  'java'
])
param runtime string = 'dotnet-isolated'

@description('Storage-account for this Function')
param storageAccountName string

@description('Storage-account id')
param storageAccountId string

@description('application-insights id')
param applicationInsightsId string

@description('Target language version used by the function app.')
@allowed([ '8.0', '9.0', '10.0'])
param runtimeVersion string = '10.0' 

@description('Storage-container-blob-endpoint for this Function')
param storageUrl string

@description('Application-Insights Connection-String for this Function')
param applicationInsightsConnectionString string

@description('Suffix to use for resources')
param suffix string = uniqueString(resourceGroup().id)

@description('Enable public access')
param publicNetworkAccess bool = false

@description('App-Settings for the Function')
param appSettings object = {}

@description('The memory size of instances used by the app.')
@allowed([2048,4096])
param instanceMemoryMB int = 2048

var functionAppName = '${name}-${suffix}'
var hostingPlanName = '${name}-plan-${suffix}'

var storageKey= listKeys(storageAccountId, '2022-05-01').keys[0].value

var storageBlobDataOwnerRoleId  = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageQueueDataContributorId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var monitoringMetricsPublisherId = '3913510d-42f4-4e42-8a64-420c390055eb'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${functionAppName}=user-assigned-identity-data-owner'
  location: location
}

resource roleAssignmentBlobDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storagstorageAccountId, userAssignedIdentity.id, 'Storage Blob Data Owner')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleAssignmentBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storageAccountId, userAssignedIdentity.id, 'Storage Blob Data Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleAssignmentQueueStorage 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storageAccountId, userAssignedIdentity.id, 'Storage Queue Data Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorId)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleAssignmentTableStorage 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storageAccountId, userAssignedIdentity.id, 'Storage Table Data Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorId)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleAssignmentAppInsights 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, applicationInsightsId, userAssignedIdentity.id, 'Monitoring Metrics Publisher')
  scope: applicationInsights
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherId)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}





resource hostingPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  properties: {
    reserved: true
    httpsOnly: true
    publicNetworkAccess: publicNetworkAccess?'Enabled':null
    serverFarmId: hostingPlan.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: storageUrl
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 40
        instanceMemoryMB: instanceMemoryMB
      }
      runtime: { 
        name: runtime
        version: runtimeVersion
      }
    }
  }
}

module functionAppSetings 'app-settings.bicep' = {
  name: '${deployment().name}-appsettings'
  params: {
    currentAppSettings: list('${functionApp.id}/config/appsettings', '2020-12-01').properties
    appSettings: union({
        AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageKey}'
        APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsightsConnectionString
        FUNCTIONS_EXTENSION_VERSION: '~4'
        WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED: '1'
    }, appSettings)
    functionName: functionApp.name
  }
}
