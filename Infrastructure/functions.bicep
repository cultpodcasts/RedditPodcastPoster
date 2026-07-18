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
@secure()
param youTubeApiKey14 string
@secure()
param youTubeApiKey15 string
@secure()
param youTubeApiKey16 string

@description('Enable provisioning of budget and monitoring alerts.')
param enableAlerts bool = true

@description('Optional email address for alert notifications. Leave empty to rely on role notifications only.')
param alertEmailAddress string = ''

resource storage 'Microsoft.Storage/storageAccounts@2022-05-01' existing = {
  name: storageName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: 'ai-${suffix}'
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: 'loganalytics-${suffix}'
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
    Logging__LogLevel__Azure: loggingLevel
    Logging__LogLevel__RedditPodcastPoster: 'Warning'
    Logging__LogLevel__Indexer: 'Information'
    Logging__LogLevel__Api: 'Information'
    Logging__LogLevel__Discovery: 'Information'
    // Legacy App Insights sampling — ignored when host.json telemetryMode is OpenTelemetry.
    Logging__ApplicationInsights__SamplingSettings__IsEnabled: 'true'
    Logging__ApplicationInsights__SamplingSettings__ExcludedTypes: ''
    Logging__ApplicationInsights__EnableLiveMetricsFilters: 'true'
    OTEL_TRACES_SAMPLER: 'microsoft.fixed_percentage'
    OTEL_TRACES_SAMPLER_ARG: '0.25'
    APPLICATIONINSIGHTS_SAMPLING_PERCENTAGE: '25'
}

var memoryProbe = {
    memoryProbe__Enabled: 'false'
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
    bluesky__MaxFailures: 5
    bluesky__MaxPosts: 5
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
    content__PeopleKey: 'people'
}

var cosmosdb= {
    cosmosdb__AuthKeyOrResourceToken: cosmosdbAuthKeyOrResourceToken
    cosmosdb__DatabaseId: 'cultpodcasts-db'
    cosmosdb__Endpoint: cosmosdbEndpoint
    cosmosdb__PodcastsContainer: 'Podcasts'
    cosmosdb__EpisodesContainer: 'Episodes'
    cosmosdb__SubjectsContainer: 'Subjects'
    cosmosdb__PeopleContainer: 'People'
    cosmosdb__ActivitiesContainer: 'Activity'
    cosmosdb__DiscoveryContainer: 'Discovery'
    cosmosdb__LookUpsContainer: 'LookUps'
    cosmosdb__PushSubscriptionsContainer: 'PushSubscriptions'
    cosmosdb__UseGateway: 'false'
}

var delayedPublication= {
    delayedYouTubePublication__EvaluationThreshold: '6:00:00'
}

var youtubeChannel= {
    youtubeChannel__PreferUploadsPlaylist: 'true'
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
    discover__LookbackMode: 'Dynamic'
    discover__DynamicLookbackOverlap: '00:10:00'
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
    postingCriteria__RedditDays: '2'
    postingCriteria__BlueSkyDays: '2'
    postingCriteria__CategoriserDays: '2'
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
    activities__RunPoster: 'false'
    activities__RunPublisher: 'true'
    activities__RunTweet: 'false'
    activities__RunBluesky: 'true'
}

var indexerTriggers = {
    AzureWebJobs_HalfHourly_Disabled: 'true'
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
    youtube__Applications__13__ApiKey: youTubeApiKey15
    youtube__Applications__14__ApiKey: youTubeApiKey14
    youtube__Applications__15__ApiKey: youTubeApiKey16
    youtube__Applications__16__ApiKey: youTubeApiKey14
}

var youTubeKeyUsage= {
    youtube__Applications__0__Name: 'CultPodcasts'
    youtube__Applications__0__Usage: 'Cli'
    youtube__Applications__0__DisplayName: 'ApiKey-0 - Cli'
    youtube__Applications__1__Name: 'CultPodcasts'
    youtube__Applications__1__Usage: 'Indexer'
    // Indexer keys form one flat rotation ring (config order, deduped by ApiKey).
    youtube__Applications__1__DisplayName: 'Indexer-Key-01-CultPodcasts'
    youtube__Applications__2__Name: 'CultPodcasts'
    youtube__Applications__2__Usage: 'Indexer'
    youtube__Applications__2__DisplayName: 'Indexer-Key-02-CultPodcasts'
    youtube__Applications__3__Name: 'CultPodcasts'
    youtube__Applications__3__Usage: 'Indexer'
    youtube__Applications__3__DisplayName: 'Indexer-Key-03-CultPodcasts'
    youtube__Applications__4__Name: 'CultPodcasts'
    youtube__Applications__4__Usage: 'Indexer'
    youtube__Applications__4__DisplayName: 'Indexer-Key-04-CultPodcasts'
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
    youtube__Applications__8__DisplayName: 'Indexer-Key-05-CultPodcasts'
    youtube__Applications__9__Name: 'CultPodcasts'
    youtube__Applications__9__Usage: 'Indexer'
    youtube__Applications__9__DisplayName: 'Indexer-Key-06-CultPodcasts'
    youtube__Applications__10__Name: 'CultPodcasts'
    youtube__Applications__10__Usage: 'Indexer'
    youtube__Applications__10__DisplayName: 'Indexer-Key-07-CultPodcasts'
    youtube__Applications__11__Name: 'CultPodcasts'
    youtube__Applications__11__Usage: 'Indexer'
    youtube__Applications__11__DisplayName: 'Indexer-Key-08-CultPodcasts'
    youtube__Applications__12__Name: 'CultPodcasts'
    youtube__Applications__12__Usage: 'Api'
    youtube__Applications__12__DisplayName: 'ApiKey-12 - Api'
    youtube__Applications__13__Name: 'cultpodcasts'
    youtube__Applications__13__Usage: 'Indexer'
    youtube__Applications__13__DisplayName: 'Indexer-Key-09-CultPodcasts'
    youtube__Applications__14__Name: 'CultPodcasts'
    youtube__Applications__14__Usage: 'Indexer'
    youtube__Applications__14__DisplayName: 'Indexer-Key-10-CultPodcasts'
    youtube__Applications__15__Name: 'cultpodcasts'
    youtube__Applications__15__Usage: 'Indexer'
    youtube__Applications__15__DisplayName: 'Indexer-Key-11-CultPodcasts'
    youtube__Applications__16__Name: 'CultPodcasts'
    youtube__Applications__16__Usage: 'Indexer'
    youtube__Applications__16__DisplayName: 'Indexer-Key-12-CultPodcasts'
}

var coreSettings= union(
    jobHostLogging,
    logging,
    memoryProbe,
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
    postingCriteria,
    youtubeChannel
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
    indexerActivities,
    indexerTriggers,
    youtubeChannel
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

resource monitoringActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (enableAlerts) {
  name: 'functions-alerts-${suffix}'
  location: 'global'
  properties: {
    enabled: true
    groupShortName: 'fn${take(suffix, 10)}'
    emailReceivers: empty(alertEmailAddress) ? [] : [
      {
        name: 'primaryEmail'
        emailAddress: alertEmailAddress
        useCommonAlertSchema: true
      }
    ]
    armRoleReceivers: [
      {
        name: 'ownerRoleReceiver'
        roleId: '8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
        useCommonAlertSchema: true
      }
    ]
  }
}

resource outOfMemoryAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = if (enableAlerts) {
  name: 'functions-oom-alert-${suffix}'
  location: location
  properties: {
    displayName: 'Functions OutOfMemory signals'
    description: 'OutOfMemory signals detected in Functions telemetry.'
    enabled: true
    scopes: [
      logAnalyticsWorkspace.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    severity: 2
    autoMitigate: true
    criteria: {
      allOf: [
        {
          query: 'union isfuzzy=true AppExceptions, AppTraces | where TimeGenerated > ago(5m) | where tostring(column_ifexists("Message", "")) has "OutOfMemory" or tostring(column_ifexists("OuterMessage", "")) has "OutOfMemory" or tostring(column_ifexists("ExceptionType", "")) has "OutOfMemory" | project TimeGenerated'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        monitoringActionGroup.id
      ]
      customProperties: {
        category: 'OutOfMemory'
      }
    }
  }
}

resource hostDrainSpikeAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = if (enableAlerts) {
  name: 'functions-host-drain-alert-${suffix}'
  location: location
  properties: {
    displayName: 'Functions host drain spike'
    description: 'Host drain endpoint spikes detected.'
    enabled: true
    scopes: [
      logAnalyticsWorkspace.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT10M'
    severity: 2
    autoMitigate: true
    criteria: {
      allOf: [
        {
          query: 'AppRequests | where TimeGenerated > ago(10m) | where Name has "/admin/host/drain" | project TimeGenerated'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 20
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        monitoringActionGroup.id
      ]
      customProperties: {
        category: 'HostDrainSpike'
      }
    }
  }
}

resource failedExecutionAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = if (enableAlerts) {
  name: 'functions-failed-execution-alert-${suffix}'
  location: location
  properties: {
    displayName: 'Functions failed executions'
    description: 'Failed function executions detected in request telemetry.'
    enabled: true
    scopes: [
      logAnalyticsWorkspace.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    severity: 2
    autoMitigate: true
    criteria: {
      allOf: [
        {
          query: 'AppRequests | where TimeGenerated > ago(5m) | where Success == false | project TimeGenerated'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        monitoringActionGroup.id
      ]
      customProperties: {
        category: 'FailedExecution'
      }
    }
  }
}

var storageAccountName = storage.name
var applicationInsightsConnectionString = applicationInsights.properties.ConnectionString
var userAssignedIdentityId = userAssignedIdentity.id
var userAssignedIdentityClientId = userAssignedIdentity.properties.clientId

var discoverScorer = {
    discover__scorer__Enabled: 'true'
    discover__scorer__BlobStorageAccountName: storageAccountName
    discover__scorer__BlobContainerName: 'discovery-models'
    discover__scorer__BlobPrefix: 'current'
    discover__scorer__AutoHideThreshold: '0.05'
}

module apiFunction 'function.bicep' = {
  name: '${deployment().name}-api'
  params: {
    name: 'api'
    location: location
    applicationInsightsConnectionString: applicationInsightsConnectionString
    storageAccountName: storageAccountName
    storageUrl: '${storage.properties.primaryEndpoints.blob}api-deployment'
    runtime: runtime
    runtimeVersion: '10.0'
    suffix: suffix
    publicNetworkAccess: true
    instanceMemoryMB: 2048
    appSettings: union({
        Logging__LogLevel__Api: 'Information'
    }, apiSettings)
    userAssignedIdentityId: userAssignedIdentityId
    userAssignedIdentityClientId: userAssignedIdentityClientId
  }
}

module discoveryFunction 'function.bicep' = {
  name: '${deployment().name}-discover'
  params: {
    name: 'discover'
    location: location
    applicationInsightsConnectionString: applicationInsightsConnectionString
    storageAccountName: storageAccountName
    storageUrl: '${storage.properties.primaryEndpoints.blob}discovery-deployment'
    runtime: runtime
    runtimeVersion: '10.0'
    suffix: suffix
    publicNetworkAccess: false
    instanceMemoryMB: 2048
    appSettings: union({
        Logging__LogLevel__Discovery: 'Information'
    }, discoverySettings, discoverScorer)
    userAssignedIdentityId: userAssignedIdentityId
    userAssignedIdentityClientId: userAssignedIdentityClientId
  }
}

module indexerFunction 'function.bicep' = {
  name: '${deployment().name}-indexer'
  params: {
    name: 'indexer'
    location: location
    applicationInsightsConnectionString: applicationInsightsConnectionString
    storageAccountName: storageAccountName
    storageUrl: '${storage.properties.primaryEndpoints.blob}indexer-deployment'
    runtime: runtime
    runtimeVersion: '10.0'
    suffix: suffix
    publicNetworkAccess: false
    instanceMemoryMB: 2048
    appSettings: union({
        Logging__LogLevel__Indexer: 'Information'
    }, indexerSettings)
    userAssignedIdentityId: userAssignedIdentityId
    userAssignedIdentityClientId: userAssignedIdentityClientId
  }  
}
