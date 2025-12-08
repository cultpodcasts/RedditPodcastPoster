param currentAppSettings object 
param appSettings object
param functionName string

var appSettingsKey= '${functionName}/appSettings'

resource siteconfig 'Microsoft.Web/sites/config@2020-12-01' = {
  name: appSettingsKey
  properties: union(currentAppSettings, appSettings)
}