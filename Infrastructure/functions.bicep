@description('Location for resources.')
param location string = resourceGroup().location

@description('Suffix to use for resources')
param suffix string = uniqueString(resourceGroup().id)

@description('Name for the Storage Account')
param storageName string

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
@secure()
param youTubeApiKey6 string
@secure()
param youTubeApiKey7 string
@secure()
param youTubeApiKey8 string
@secure()
param youTubeApiKey9 string
@secure()
param youTubeApiKey10 string
@secure()
param youTubeApiKey11 string
@secure()
param youTubeApiKey12 string
@secure()
param youTubeApiKey13 string

resource storage 'Microsoft.Storage/storageAccounts@2022-05-01' existing = {
  name: storageName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: 'ai-${suffix}'
}

var loggingLevel = 'Warning'
var runtime = 'dotnet-isolated'
var auth0Audience= 'https://api.cultpodcasts.com/'
var auth0Domain= 'auth.cultpodcasts.com'
var redditUserAgent= 'CultpodcastsBot/1.0'

var jobHostLogging= {
    AzureFunctionsJobHost__Logging__ApplicationInsights__LogLevel__Default: 'Warning'
    AzureFunctionsJobHost__Logging__Console__LogLevel__Default: loggingLevel
    AzureFunctionsJobHost__Logging__Debug__LogLevel__Default: 'Warning'
    AzureFunctionsJobHost__Logging__LogLevel__Default: 'Warning'
}

var logging= {
    Logging__LogLevel__Default: 'Warning'
    'Logging__LogLevel__Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler': 'Warning'
    Logging__LogLevel__Function: loggingLevel
    Logging__LogLevel__RedditPodcastPoster: loggingLevel
    Logging__ApplicationInsights__SamplingSettings__IsEnabled: 'true'
    Logging__ApplicationInsights__SamplingSettings__ExcludedTypes: ''
    Logging__ApplicationInsights__EnableLiveMetricsFilters: 'true'
}

var api= {
    api__Endpoint: 'https://api.cultpodcasts.com'
}

var auth0= {
    auth0__Audience: auth0Audience
    auth0__Domain: auth0Domain
    auth0__Issuer: 'https://auth.cultpodcasts.com/'
}

var auth0Client= {
    auth0client__Audience: auth0Audience
    auth0client__ClientId: auth0ClientId
    auth0client__ClientSecret: auth0ClientSecret
    auth0client__Domain: auth0Domain
}

var bluesky= {
    bluesky__HashTag: 'Cult'
    bluesky__Identifier: 'cultpodcasts.com'
    bluesky__Password: blueskyPassword
    bluesky__ReuseSession: 'true'
    bluesky__WithEpisodeUrl: 'true'
}

var cloudflare= {
    cloudflare__AccountId: cloudflareAccountId
    cloudflare__KVApiToken: cloudflareKVApiToken
    cloudflare__R2AccessKey: cloudflareR2AccessKey
    cloudflare__R2SecretKey: cloudflareR2SecretKey
}

var content= {
    content__BucketName: 'content'
    content__DiscoveryInfoKey: 'discovery-info'
    content__FlairsKey: 'flairs'
    content__HomepageKey: 'homepage'
    content__LanguagesKey: 'languages'
    content__PreProcessedHomepageKey: 'homepage-ssr'
    content__SubjectsKey: 'subjects'
}

var cosmosdb= {
    cosmosdb__AuthKeyOrResourceToken: cosmosdbAuthKeyOrResourceToken
    cosmosdb__Container: 'cultpodcasts'
    cosmosdb__DatabaseId: 'cultpodcasts'
    cosmosdb__Endpoint: cosmosdbEndpoint
    cosmosdb__UseGateWay: 'false'
}

var delayedPublication= {
    delayedYouTubePublication__EvaluationThreshold: '6:00:00'
}

var discover= {
    discover__EnrichFromApple: 'true'
    discover__EnrichFromSpotify: 'true'
    discover__ExcludeSpotify: 'false'
    discover__IgnoreTerms__0: 'cult of the lamb'
    discover__IgnoreTerms__1: 'cult of lamb'
    discover__IgnoreTerms__2: 'COTL'
    discover__IgnoreTerms__3: 'cult of the lab'
    discover__IgnoreTerms__4: 'Cult of the Lamp'
    discover__IgnoreTerms__5: 'Cult of the Lumb'
    discover__IgnoreTerms__6: 'Blue Oyster Cult'
    discover__IgnoreTerms__7: 'Blue Öyster Cult'
    discover__IgnoreTerms__8: 'Living Colour'
    discover__IgnoreTerms__9: 'She Sells Sanctuary'
    discover__IgnoreTerms__10: 'Far Cry'
    discover__IgnoreTerms__11: 'cult classic'
    discover__IgnoreTerms__12: 'cult film'
    discover__IgnoreTerms__13: 'cult movie'
    discover__IgnoreTerms__14: 'cult cinema'
    discover__IgnoreTerms__15: 'cult-classic'
    discover__IgnoreTerms__16: 'cult-film'
    discover__IgnoreTerms__17: 'cult-movie'
    discover__IgnoreTerms__18: 'cult-cinema'
    discover__IncludeListenNotes: 'true'
    discover__IncludeTaddy: 'true'
    discover__IncludeYouTube: 'true'
    discover__Queries__0__DiscoverService: 'ListenNotes'
    discover__Queries__0__Term: 'Cult'
    discover__Queries__1__DiscoverService: 'ListenNotes'
    discover__Queries__1__Term: 'Cults'
    discover__Queries__2__DiscoverService: 'Spotify'
    discover__Queries__2__Term: 'Cult'
    discover__Queries__3__DiscoverService: 'Spotify'
    discover__Queries__3__Term: 'Cults'
    discover__Queries__4__DiscoverService: 'YouTube'
    discover__Queries__4__Term: 'Cult'
    discover__Queries__5__DiscoverService: 'YouTube'
    discover__Queries__5__Term: 'Cults'
    discover__Queries__6__DiscoverService: 'Taddy'
    discover__Queries__6__Term: 'Cult'
    discover__SearchSince: '6:10:00'
    discover__TaddyOffset: '2:00:00'
}

var indexer= {
    indexer__ByPassYouTube: false
    indexer__ReleasedDaysAgo: '2'
}

var listenNotes= {
    listenNotes__RequestDelaySeconds: '2'
    listenNotes__Key: listenNotesKey
}

var poster= {
    poster__ReleasedDaysAgo: '4'
    poster__MaxPosts: '15'
}

var postingCriteria= {
    postingCriteria__minimumDuration: '0:9:00'
    postingCriteria__TweetDays: '2'
}

var pushSubscriptions= {
    pushSubscriptions__PrivateKey: pushSubscriptionsPrivateKey
    pushSubscriptions__PublicKey: pushSubscriptionsPublicKey
    pushSubscriptions__Subject: 'mailto:vapid@cultpodcasts.com'
}

var reddit= {
    reddit__AppId: redditAppId
    reddit__AppSecret: redditAppSecret
    reddit__RefreshToken: redditRefreshToken
    reddit__UserAgent: redditUserAgent
}

var redditAdmin= {
    redditAdmin__AppId: redditAdminAppId
    redditAdmin__AppSecret: redditAdminAppSecret
    redditAdmin__RefreshToken: redditAdminRefreshToken
    redditAdmin__UserAgent: redditUserAgent
}

var redirect= {
    redirect__KVRedirectNamespaceId: '19eea88f0cb14548bcab925238a68cc4'
}

var searchIndex= {
    searchIndex__IndexerName: 'cultpodcasts-indexer'
    searchIndex__IndexName: 'cultpodcasts'
    searchIndex__Key: searchIndexKey
    searchIndex__Url: searchIndexUrl
}

var shortner= {
    shortner__KVShortnerNamespaceId: '663cd5c74988404dafbf67e1e06b21e8'
    shortner__ShortnerUrl: 'https://s.cultpodcasts.com'
}

var spotify= {
    spotify__ClientId: spotifyClientId
    spotify__ClientSecret: spotifyClientSecret
}

var subreddit= {
    subreddit__SubredditName: 'cultpodcasts'
    subreddit__SubredditTitleMaxLength: '300'
}

var taddy= {
    taddy__ApiKey: taddyApiKey
    taddy__Userid: taddyUserId
}

var indexerActivities= {
    activities__RunIndex: 'true'
    activities__RunCategoriser: 'true'
    activities__RunPoster: 'true'
    activities__RunPublisher: 'true'
    activities__RunTweet: 'true'
    activities__RunBluesky: 'true'
}

var textanalytics= {
    textanalytics__ApiKey: textanalyticsApiKey
    textanalytics__EndPoint: textanalyticsEndPoint
}

var twitter= {
    twitter__AccessToken: twitterAccessToken
    twitter__AccessTokenSecret: twitterAccessTokenSecret
    twitter__ConsumerKey: twitterConsumerKey
    twitter__ConsumerSecret: twitterConsumerSecret
    twitter__HashTag: 'Cult'
    twitter__TwitterId: twitterTwitterId
    twitter__WithEpisodeUrl: 'true'
}

var youtube= {
    youtube__Applications__0__ApiKey: youTubeApiKey0
    youtube__Applications__1__ApiKey: youTubeApiKey1
    youtube__Applications__2__ApiKey: youTubeApiKey2
    youtube__Applications__3__ApiKey: youTubeApiKey3
    youtube__Applications__4__ApiKey: youTubeApiKey4
    youtube__Applications__5__ApiKey: youTubeApiKey5
    youtube__Applications__6__ApiKey: youTubeApiKey6
    youtube__Applications__7__ApiKey: youTubeApiKey7
    youtube__Applications__8__ApiKey: youTubeApiKey8
    youtube__Applications__9__ApiKey: youTubeApiKey9
    youtube__Applications__10__ApiKey: youTubeApiKey10
    youtube__Applications__11__ApiKey: youTubeApiKey11
    youtube__Applications__12__ApiKey: youTubeApiKey12
    youtube__Applications__13__ApiKey: youTubeApiKey13
    youtube__Applications__14__ApiKey: youTubeApiKey13
    youtube__Applications__15__ApiKey: youTubeApiKey13
    youtube__Applications__16__ApiKey: youTubeApiKey13
}

var youTubeKeyUsage= {
    youtube__Applications__0__Name: 'CultPodcasts'
    youtube__Applications__0__Usage: 'Cli'
    youtube__Applications__0__DisplayName: 'ApiKey-0 - Cli'
    youtube__Applications__1__Name: 'CultPodcasts'
    youtube__Applications__1__Usage: 'Indexer'
    youtube__Applications__1__DisplayName: 'ApiKey-1 - Indexer'
    youtube__Applications__2__Name: 'CultPodcasts'
    youtube__Applications__2__Usage: 'Indexer'
    youtube__Applications__2__DisplayName: 'ApiKey-2 - Indexer'
    youtube__Applications__3__Name: 'CultPodcasts'
    youtube__Applications__3__Usage: 'Indexer'
    youtube__Applications__3__DisplayName: 'ApiKey-3 - Indexer'
    youtube__Applications__4__Name: 'CultPodcasts'
    youtube__Applications__4__Usage: 'Indexer'
    youtube__Applications__4__DisplayName: 'ApiKey-4 - Indexer'
    youtube__Applications__5__Usage: 'Discover'
    youtube__Applications__5__Name: 'CultPodcasts'
    youtube__Applications__5__DisplayName: 'ApiKey-5 - Discover'
    youtube__Applications__6__Usage: 'Discover'
    youtube__Applications__6__Name: 'CultPodcasts'
    youtube__Applications__6__DisplayName: 'ApiKey-6 - Discover'
    youtube__Applications__7__Usage: 'Bluesky'
    youtube__Applications__7__Name: 'CultPodcasts'
    youtube__Applications__7__DisplayName: 'ApiKey-7 - Bluesky'
    youtube__Applications__8__Name: 'CultPodcasts'
    youtube__Applications__8__Usage: 'Indexer'
    youtube__Applications__8__DisplayName: 'ApiKey-8 (Reattempt 1 in place of Api-Key-1) - Indexer'
    youtube__Applications__8__Reattempt: '1'
    youtube__Applications__9__Name: 'CultPodcasts'
    youtube__Applications__9__Usage: 'Indexer'
    youtube__Applications__9__DisplayName: 'ApiKey-9 (Reattempt 1 in place of Api-Key-2) - Indexer'
    youtube__Applications__9__Reattempt: '1'
    youtube__Applications__10__Name: 'CultPodcasts'
    youtube__Applications__10__Usage: 'Indexer'
    youtube__Applications__10__DisplayName: 'ApiKey-10 (Reattempt 1 in place of Api-Key-3) - Indexer'
    youtube__Applications__10__Reattempt: '1'
    youtube__Applications__11__Name: 'CultPodcasts'
    youtube__Applications__11__Usage: 'Indexer'
    youtube__Applications__11__DisplayName: 'ApiKey-11 (Reattempt 1 in place of Api-Key-4) - Indexer'
    youtube__Applications__11__Reattempt: '1'
    youtube__Applications__12__Name: 'CultPodcasts'
    youtube__Applications__12__Usage: 'Api'
    youtube__Applications__12__DisplayName: 'ApiKey-12 - Api'
    youtube__Applications__13__Name: 'cultcodcasts'
    youtube__Applications__13__Usage: 'Indexer'
    youtube__Applications__13__DisplayName: 'ApiKey-13 (Reattempt 2 in place of Api-Key-1 & 8) - Indexer'
    youtube__Applications__13__Reattempt: '2'
    youtube__Applications__14__Name: 'cultcodcasts'
    youtube__Applications__14__Usage: 'Indexer'
    youtube__Applications__14__DisplayName: 'ApiKey-14 (Reattempt 2 in place of Api-Key-2 & 9) - Indexer'
    youtube__Applications__14__Reattempt: '2'
    youtube__Applications__15__Name: 'cultcodcasts'
    youtube__Applications__15__Usage: 'Indexer'
    youtube__Applications__15__DisplayName: 'ApiKey-15 (Reattempt 2 in place of Api-Key-3 & 10) - Indexer'
    youtube__Applications__15__Reattempt: '2'
    youtube__Applications__16__Name: 'cultcodcasts'
    youtube__Applications__16__Usage: 'Indexer'
    youtube__Applications__16__DisplayName: 'ApiKey-16 (Reattempt 2 in place of Api-Key-4 & 11) - Indexer'
    youtube__Applications__16__Reattempt: '2'
}

var coreSettings= union(
    jobHostLogging,
    logging,
    api,
    auth0Client, 
    bluesky, 
    cloudflare, 
    content,
    cosmosdb, 
    delayedPublication,
    pushSubscriptions, 
    reddit, 
    redditAdmin, 
    redirect, 
    searchIndex, 
    shortner,
    spotify, 
    subreddit,
    textanalytics, 
    twitter, 
    youtube, 
    youTubeKeyUsage
)

var apiSettings= union(
    coreSettings,
    auth0,
    indexer,
    postingCriteria
)

var discoverySettings= union(
    coreSettings,
    discover,
    listenNotes,
    taddy
)

var indexerSettings= union(
    coreSettings,
    indexer,
    poster,
    postingCriteria,
    indexerActivities
 )

var storageBlobDataOwnerRoleId  = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageQueueDataContributorId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var monitoringMetricsPublisherId = '3913510d-42f4-4e42-8a64-420c390055eb'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'user-assigned-identity-data-owner'
  location: location
}

resource roleAssignmentBlobDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storage.id, userAssignedIdentity.id, 'Storage Blob Data Owner')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleAssignmentBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storage.id, userAssignedIdentity.id, 'Storage Blob Data Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleAssignmentQueueStorage 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storage.id, userAssignedIdentity.id, 'Storage Queue Data Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorId)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleAssignmentTableStorage 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storage.id, userAssignedIdentity.id, 'Storage Table Data Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorId)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleAssignmentAppInsights 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, applicationInsights.id, userAssignedIdentity.id, 'Monitoring Metrics Publisher')
  scope: applicationInsights
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherId)
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

module apiFunction 'function.bicep' = {
  name: '${deployment().name}-api'
  params: {
    name: 'api'
    location: location
    applicationInsightsConnectionString: applicationInsights.properties.ConnectionString
    storageAccountName: storage.name
    storageUrl: '${storage.properties.primaryEndpoints.blob}api-deployment'
    runtime: runtime
    runtimeVersion: '10.0'
    suffix: suffix
    publicNetworkAccess: true
    instanceMemoryMB: 2048
    appSettings: union({
        Logging__LogLevel__Api: loggingLevel
    }, apiSettings)
    userAssignedIdentityId: userAssignedIdentity.id
    userAssignedIdentityClientId: userAssignedIdentity.properties.clientId
  }
}

module discoveryFunction 'function.bicep' = {
  name: '${deployment().name}-discover'
  params: {
    name: 'discover'
    location: location
    applicationInsightsConnectionString: applicationInsights.properties.ConnectionString
    storageAccountName: storage.name
    storageUrl: '${storage.properties.primaryEndpoints.blob}discovery-deployment'
    runtime: runtime
    runtimeVersion: '10.0'
    suffix: suffix
    publicNetworkAccess: false
    instanceMemoryMB: 2048
    appSettings: union({
        Logging__LogLevel__Discovery: loggingLevel
    }, discoverySettings)
    userAssignedIdentityId: userAssignedIdentity.id
    userAssignedIdentityClientId: userAssignedIdentity.properties.clientId
  }
}

module indexerFunction 'function.bicep' = {
  name: '${deployment().name}-indexer'
  params: {
    name: 'indexer'
    location: location
    applicationInsightsConnectionString: applicationInsights.properties.ConnectionString
    storageAccountName: storage.name
    storageUrl: '${storage.properties.primaryEndpoints.blob}indexer-deployment'
    runtime: runtime
    runtimeVersion: '10.0'
    suffix: suffix
    publicNetworkAccess: false
    instanceMemoryMB: 2048
    appSettings: union({
        Logging__LogLevel__Indexer: loggingLevel
    }, indexerSettings)
    userAssignedIdentityId: userAssignedIdentity.id
    userAssignedIdentityClientId: userAssignedIdentity.properties.clientId
  }  
}