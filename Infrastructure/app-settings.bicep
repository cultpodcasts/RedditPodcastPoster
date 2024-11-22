param currentAppSettings object 
param appSettings object
param functionName string

resource siteconfig 'Microsoft.Web/sites/config@2024-04-01' = {
  name: '${functionName}/appSettings'
  properties: union(currentAppSettings, appSettings)
}