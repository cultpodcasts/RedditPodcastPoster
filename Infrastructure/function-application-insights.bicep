@description('Location for Application Insights')
param location string = resourceGroup().location

@description('Application Insights WorkspaceResourceId')
param workspaceResourceId string

var applicationInsightsName = 'ai-${uniqueString(resourceGroup().id)}'

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest',
    Flow_Type: 'Redfield',
    Request_Source: 'IbizaWebAppExtensionCreate',
    WorkspaceResourceId: workspaceResourceId
  }
}