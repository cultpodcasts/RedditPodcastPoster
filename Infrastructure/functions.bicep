@description('Location for resources.')
param location string = resourceGroup().location

@description('Suffix to use for resources')
param suffix string = uniqueString(resourceGroup().id)

@description('Name for the Storage Account')
param storageName string

@description('Runtime for the Functions')
@allowed([
  'dotnet-isolated'
  'node'
  'dotnet'
  'java'
])
param runtime string

@description('Enabled-state for the function app.')
param functionEnabled bool

@description('App-Settings for Api-Function')
param apiSettings object = {}
@description('App-Settings for Discover-Function')
param discoverySettings object = {}
@description('App-Settings for Indexer-Function')
param indexerSettings object = {}

@secure()
param auth0ClientId string
@secure()
param auth0ClientSecret string
@secure()
param blueskyPassword string
@secure()
param cloudflareAccountId string
@secure()
param cloudflareKVApiToken string
@secure()
param cloudflareListsApiToken string
@secure()
param cloudflareR2AccessKey string
@secure()
param cloudflareR2SecretKey string
@secure()
param cosmosdbAuthKeyOrResourceToken string
@secure()
param cosmosdbEndpoint string
@secure()
param listenNotesKey string
@secure()
param pushSubscriptionsPrivateKey string
@secure()
param pushSubscriptionsPublicKey string
@secure()
param redditAppId string
@secure()
param redditAppSecret string
@secure()
param redditRefreshToken string
@secure()
param redditAdminAppId string
@secure()
param redditAdminAppSecret string
@secure()
param redditAdminRefreshToken string
@secure()
param redirectsPodcastRedirectRulesId string
@secure()
param searchIndexKey string
@secure()
param searchIndexUrl string
@secure()
param spotifyClientId string
@secure()
param spotifyClientSecret string
@secure()
param taddyApiKey string
@secure()
param taddyUserId string
@secure()
param textanalyticsApiKey string
@secure()
param textanalyticsEndPoint string
@secure()
param twitterAccessToken string
@secure()
param twitterAccessTokenSecret string
@secure()
param twitterConsumerKey string
@secure()
param twitterConsumerSecret string
@secure()
param twitterTwitterId string
@secure()
param youTubeApiKey0 string
@secure()
param youTubeApiKey1 string
@secure()
param youTubeApiKey2 string
@secure()
param youTubeApiKey3 string
@secure()
param youTubeApiKey4 string
@secure()
param youTubeApiKey5 string

resource storage 'Microsoft.Storage/storageAccounts@2022-05-01' existing = {
  name: storageName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: 'ai-${suffix}'
}

module apiFunction 'function.bicep' = {
  name: '${deployment().name}-api'
  params: {
    name: 'api'
    location: location
    applicationInsightsConnectionString: applicationInsights.properties.ConnectionString
    storageAccountName: storage.name
    storageAccountId: storage.id
    runtime: runtime
    suffix: suffix
    publicNetworkAccess: true
    appSettings: union({
        auth0client__ClientId: auth0ClientId
        auth0client__ClientSecret: auth0ClientSecret
        bluesky__Password: blueskyPassword
        cloudflare__AccountId: cloudflareAccountId
        cloudflare__KVApiToken: cloudflareKVApiToken
        cloudflare__ListsApiToken: cloudflareListsApiToken
        cloudflare__R2AccessKey: cloudflareR2AccessKey
        cloudflare__R2SecretKey: cloudflareR2SecretKey
        cosmosdb__AuthKeyOrResourceToken: cosmosdbAuthKeyOrResourceToken
        cosmosdb__Endpoint: cosmosdbEndpoint
        listenNotes__Key: listenNotesKey
        pushSubscriptions__PrivateKey: pushSubscriptionsPrivateKey
        pushSubscriptions__PublicKey: pushSubscriptionsPublicKey
        reddit__AppId: redditAppId
        reddit__AppSecret: redditAppSecret
        reddit__RefreshToken: redditRefreshToken
        redditAdmin__AppId: redditAdminAppId
        redditAdmin__AppSecret: redditAdminAppSecret
        redditAdmin__RefreshToken: redditAdminRefreshToken
        redirects__PodcastRedirectRulesId: redirectsPodcastRedirectRulesId
        searchIndex__Key: searchIndexKey
        searchIndex__Url: searchIndexUrl
        spotify__ClientId: spotifyClientId
        spotify__ClientSecret: spotifyClientSecret
        taddy__ApiKey: taddyApiKey
        taddy__Userid: taddyUserId
        textanalytics__ApiKey: textanalyticsApiKey
        textanalytics__EndPoint: textanalyticsEndPoint
        twitter__AccessToken: twitterAccessToken
        twitter__AccessTokenSecret: twitterAccessTokenSecret
        twitter__ConsumerKey: twitterConsumerKey
        twitter__ConsumerSecret: twitterConsumerSecret
        twitter__TwitterId: twitterTwitterId
        youtube__Applications__0__ApiKey: youTubeApiKey0
        youtube__Applications__1__ApiKey: youTubeApiKey1
    }, apiSettings)
  }
}

module discoveryFunction 'function.bicep' = {
  name: '${deployment().name}-discover'
  params: {
    name: 'discover'
    location: location
    functionEnabled: functionEnabled
    applicationInsightsConnectionString: applicationInsights.properties.ConnectionString
    storageAccountName: storage.name
    storageAccountId: storage.id
    runtime: runtime
    suffix: suffix
    publicNetworkAccess: false
    appSettings: union({
        auth0client__ClientId: auth0ClientId
        auth0client__ClientSecret: auth0ClientSecret
        bluesky__Password: blueskyPassword
        cloudflare__AccountId: cloudflareAccountId
        cloudflare__KVApiToken: cloudflareKVApiToken
        cloudflare__ListsApiToken: cloudflareListsApiToken
        cloudflare__R2AccessKey: cloudflareR2AccessKey
        cloudflare__R2SecretKey: cloudflareR2SecretKey
        cosmosdb__AuthKeyOrResourceToken: cosmosdbAuthKeyOrResourceToken
        cosmosdb__Endpoint: cosmosdbEndpoint
        listenNotes__Key: listenNotesKey
        pushSubscriptions__PrivateKey: pushSubscriptionsPrivateKey
        pushSubscriptions__PublicKey: pushSubscriptionsPublicKey
        reddit__AppId: redditAppId
        reddit__AppSecret: redditAppSecret
        reddit__RefreshToken: redditRefreshToken
        redditAdmin__AppId: redditAdminAppId
        redditAdmin__AppSecret: redditAdminAppSecret
        redditAdmin__RefreshToken: redditAdminRefreshToken
        searchIndex__Key: searchIndexKey
        searchIndex__Url: searchIndexUrl
        spotify__ClientId: spotifyClientId
        spotify__ClientSecret: spotifyClientSecret
        taddy__ApiKey: taddyApiKey
        taddy__Userid: taddyUserId
        textanalytics__ApiKey: textanalyticsApiKey
        textanalytics__EndPoint: textanalyticsEndPoint
        twitter__AccessToken: twitterAccessToken
        twitter__AccessTokenSecret: twitterAccessTokenSecret
        twitter__ConsumerKey: twitterConsumerKey
        twitter__ConsumerSecret: twitterConsumerSecret
        twitter__TwitterId: twitterTwitterId
        youtube__Applications__0__ApiKey: youTubeApiKey2
        youtube__Applications__1__ApiKey: youTubeApiKey3
        youtube__Applications__2__ApiKey: youTubeApiKey4
        youtube__Applications__3__ApiKey: youTubeApiKey5
    }, discoverySettings)
  }
}

module indexerFunction 'function.bicep' = {
  name: '${deployment().name}-indexer'
  params: {
    name: 'indexer'
    location: location
    functionEnabled: functionEnabled
    applicationInsightsConnectionString: applicationInsights.properties.ConnectionString
    storageAccountName: storage.name
    storageAccountId: storage.id
    runtime: runtime
    suffix: suffix
    publicNetworkAccess: false
    appSettings: union({
        auth0client__ClientId: auth0ClientId
        auth0client__ClientSecret: auth0ClientSecret
        bluesky__Password: blueskyPassword
        cloudflare__AccountId: cloudflareAccountId
        cloudflare__KVApiToken: cloudflareKVApiToken
        cloudflare__ListsApiToken: cloudflareListsApiToken
        cloudflare__R2AccessKey: cloudflareR2AccessKey
        cloudflare__R2SecretKey: cloudflareR2SecretKey
        cosmosdb__AuthKeyOrResourceToken: cosmosdbAuthKeyOrResourceToken
        cosmosdb__Endpoint: cosmosdbEndpoint
        listenNotes__Key: listenNotesKey
        pushSubscriptions__PrivateKey: pushSubscriptionsPrivateKey
        pushSubscriptions__PublicKey: pushSubscriptionsPublicKey
        reddit__AppId: redditAppId
        reddit__AppSecret: redditAppSecret
        reddit__RefreshToken: redditRefreshToken
        redditAdmin__AppId: redditAdminAppId
        redditAdmin__AppSecret: redditAdminAppSecret
        redditAdmin__RefreshToken: redditAdminRefreshToken
        searchIndex__Key: searchIndexKey
        searchIndex__Url: searchIndexUrl
        spotify__ClientId: spotifyClientId
        spotify__ClientSecret: spotifyClientSecret
        taddy__ApiKey: taddyApiKey
        taddy__Userid: taddyUserId
        textanalytics__ApiKey: textanalyticsApiKey
        textanalytics__EndPoint: textanalyticsEndPoint
        twitter__AccessToken: twitterAccessToken
        twitter__AccessTokenSecret: twitterAccessTokenSecret
        twitter__ConsumerKey: twitterConsumerKey
        twitter__ConsumerSecret: twitterConsumerSecret
        twitter__TwitterId: twitterTwitterId
        youtube__Applications__0__ApiKey: youTubeApiKey2
        youtube__Applications__1__ApiKey: youTubeApiKey3
        youtube__Applications__2__ApiKey: youTubeApiKey4
        youtube__Applications__3__ApiKey: youTubeApiKey5
    }, indexerSettings)
  }  
}


