using './functions.bicep'

param runtime = 'dotnet-isolated'

param auth0ClientId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Auth0-ClientId')
param auth0ClientSecret= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Auth0-ClientSecret')
param blueskyPassword= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Bluesky-Password')
param cloudflareAccountId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Cloudflare-AccountId')
param cloudflareKVApiToken= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Cloudflare-KVApiToken')
param cloudflareR2AccessKey= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Cloudflare-R2AccessKey')
param cloudflareR2SecretKey= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Cloudflare-R2SecretKey')
param cosmosdbAuthKeyOrResourceToken= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Cosmosdb-AuthKeyOrResourceToken')
param cosmosdbEndpoint= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Cosmosdb-Endpoint')
param listenNotesKey= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'ListenNotes-Key')
param pushSubscriptionsPrivateKey= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'PushSubscriptions-PrivateKey')
param pushSubscriptionsPublicKey= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'PushSubscriptions-PublicKey')
param redditAppId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Reddit-AppId')
param redditAppSecret= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Reddit-AppSecret')
param redditRefreshToken= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Reddit-RefreshToken')
param redditAdminAppId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'RedditAdmin-AppId')
param redditAdminAppSecret= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'RedditAdmin-AppSecret')
param redditAdminRefreshToken= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'RedditAdmin-RefreshToken')
param searchIndexKey= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'SearchIndex-Key')
param searchIndexUrl= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'SearchIndex-Url')
param spotifyClientId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Spotify-ClientId')
param spotifyClientSecret= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Spotify-ClientSecret')
param taddyApiKey= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Taddy-ApiKey')
param taddyUserId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Taddy-Userid')
param textanalyticsApiKey= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Textanalytics-ApiKey')
param textanalyticsEndPoint= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Textanalytics-EndPoint')
param twitterAccessToken= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Twitter-AccessToken')
param twitterAccessTokenSecret= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Twitter-AccessTokenSecret')
param twitterConsumerKey= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Twitter-ConsumerKey')
param twitterConsumerSecret= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Twitter-ConsumerSecret')
param twitterTwitterId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Twitter-TwitterId')
param youTubeApiKey0= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-0')
param youTubeApiKey1= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-1')
param youTubeApiKey2= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-2')
param youTubeApiKey3= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-3')
param youTubeApiKey4= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-4')
param youTubeApiKey5= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-5')
param youTubeApiKey6= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-6')
param youTubeApiKey7= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-7')
param youTubeApiKey8= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-8')
param youTubeApiKey9= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-9')
param youTubeApiKey10= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Youtube-ApiKey-10')

var content= {
	content__BucketName: 'content'
	content__DiscoveryInfoKey: 'discovery-info'
	content__FlairsKey: 'flairs'
	content__HomepageKey: 'homepage'
	content__PreProcessedHomepageKey: 'homepage-ssr'
	content__SubjectsKey: 'subjects'
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
	discover__IgnoreTerms__10: 'Far Cry'
	discover__IgnoreTerms__11: 'cult classic'
	discover__IgnoreTerms__12: 'cult film'
	discover__IgnoreTerms__13: 'cult movie'
	discover__IgnoreTerms__2: 'COTL'
	discover__IgnoreTerms__3: 'cult of the lab'
	discover__IgnoreTerms__4: 'Cult of the Lamp'
	discover__IgnoreTerms__5: 'Cult of the Lumb'
	discover__IgnoreTerms__6: 'Blue Oyster Cult'
	discover__IgnoreTerms__7: 'Blue Öyster Cult'
	discover__IgnoreTerms__8: 'Living Colour'
	discover__IgnoreTerms__9: 'She Sells Sanctuary'
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

var poster= {
	poster__ReleasedDaysAgo: '4'
}

var pushSubscriptions= {
	pushSubscriptions__Subject: 'mailto:vapid@cultpodcasts.com'
}

var postingCriteria= {
	postingCriteria__minimumDuration: '0:9:00'
	postingCriteria__TweetDays: '2'
}

var redirect= {
	redirect__KVRedirectNamespaceId: '19eea88f0cb14548bcab925238a68cc4'
}

var searchIndex= {
	searchIndex__IndexerName: 'cultpodcasts-indexer'
	searchIndex__IndexName: 'cultpodcasts'
}

var shortner= {
	shortner__KVShortnerNamespaceId: '663cd5c74988404dafbf67e1e06b21e8'
	shortner__ShortnerUrl: 'https://s.cultpodcasts.com'
}

var subreddit= {
	subreddit__SubredditName: 'cultpodcasts'
	subreddit__SubredditTitleMaxLength: '300'
}

var twitter= {
	twitter__HashTag: 'Cult'
	twitter__WithEpisodeUrl: 'true'
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
	youtube__Applications__4__DisplayName: 'ApiKey-5 - Indexer'
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
	youtube__Applications__9__DisplayName: 'ApiKey-8 (Reattempt 1 in place of Api-Key-2) - Indexer'
	youtube__Applications__9__Reattempt: '1'
	youtube__Applications__10_Name: 'CultPodcasts'
	youtube__Applications__10__Usage: 'Indexer'
	youtube__Applications__10__DisplayName: 'ApiKey-9 (Reattempt 1 in place of Api-Key-4) - Indexer'
	youtube__Applications__10__Reattempt: '1'
	youtube__Applications__11__Name: 'CultPodcasts'
	youtube__Applications__11__Usage: 'Indexer'
	youtube__Applications__11__DisplayName: 'ApiKey-9 (Reattempt 1 in place of Api-Key-5) - Indexer'
	youtube__Applications__11__Reattempt: '1'
	youtube__Applications__12__Name: 'CultPodcasts'
	youtube__Applications__12__Usage: 'Api'
	youtube__Applications__12__DisplayName: 'ApiKey-10 - Cli'
}

param apiSettings = union(
	content, 
	delayedPublication, 
	indexer, 
	redirect,
	searchIndex, 
	shortner, 
	subreddit, 
	twitter, 
	youTubeKeyUsage
)

param discoverySettings= union(
	content, 
	delayedPublication, 
	discover,
	pushSubscriptions,
	redirect, 
	searchIndex, 
	shortner,
	subreddit, 
	twitter, 
	youTubeKeyUsage
)

param indexerSettings= union(
	content, 
	delayedPublication, 
	indexer, 
	poster,
	postingCriteria, 
	redirect, 
	searchIndex, 
	shortner, 
	subreddit, 
	twitter, 
	youTubeKeyUsage 
)