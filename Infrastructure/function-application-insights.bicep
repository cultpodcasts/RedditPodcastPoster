@description('Location for Application Insights')
param location string = resourceGroup().location

var applicationInsightsName = 'ai-${uniqueString(resourceGroup().id)}'

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
  }
}