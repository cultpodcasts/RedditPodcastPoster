using './functions.bicep'

param runtime = 'dotnet-isolated'

param auth0ClientId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Auth0-ClientId')
param auth0ClientSecret= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Auth0-ClientSecret')
param blueskyPassword= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Bluesky-Password')
param cloudflareAccountId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Cloudflare-AccountId')
param cloudflareKVApiToken= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Cloudflare-KVApiToken')
param cloudflareListsApiToken= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Cloudflare-ListsApiToken')
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
param redirectsPodcastRedirectRulesId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Redirects-PodcastRedirectRulesId')
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

var youTubeKeyUsage= {
	youtube__Applications__0__Name: 'CultPodcasts'
	youtube__Applications__0__Usage: 'Api,Cli'
	youtube__Applications__0__DisplayName: 'ApiKey0 - Api & Cli'
	youtube__Applications__1__Name: 'CultPodcasts'
	youtube__Applications__1__Usage: 'Indexer'
	youtube__Applications__1__DisplayName: 'ApiKey1 - Indexer'
	youtube__Applications__2__Name: 'CultPodcasts'
	youtube__Applications__2__Usage: 'Indexer'
	youtube__Applications__2__DisplayName: 'ApiKey2 - Indexer'
	youtube__Applications__3__Name: 'CultPodcasts'
	youtube__Applications__3__Usage: 'Indexer'
	youtube__Applications__3__DisplayName: 'ApiKey3 - Indexer'
	youtube__Applications__4__Name: 'CultPodcasts'
	youtube__Applications__4__Usage: 'Indexer'
	youtube__Applications__4__DisplayName: 'ApiKey5 - Indexer'
	youtube__Applications__5__Usage: 'Discover'
	youtube__Applications__5__Name: 'CultPodcasts'
	youtube__Applications__5__DisplayName: 'ApiKey5 - Discover'
	youtube__Applications__6__Usage: 'Discover'
	youtube__Applications__6__Name: 'CultPodcasts'
	youtube__Applications__6__DisplayName: 'ApiKey6 - Discover'
	youtube__Applications__7__Usage: 'Bluesky'
	youtube__Applications__7__Name: 'CultPodcasts'
	youtube__Applications__7__DisplayName: 'ApiKey7 - Bluesky'
}

param apiSettings = union(youTubeKeyUsage, {
	api__Endpoint:	'https://api.cultpodcasts.com'
	auth0__Audience: 'https://api.cultpodcasts.com/'
	auth0__Domain: 'cultpodcasts.uk.auth0.com'
	auth0__Issuer: 'https://cultpodcasts.uk.auth0.com/'
	auth0client__Audience: 'https://api.cultpodcasts.com/'
	auth0client__Domain: 'cultpodcasts.uk.auth0.com'
	bluesky__HashTag: 'Cult'
	bluesky__Identifier: 'cultpodcasts.com'
	bluesky__WithEpisodeUrl: 'true'
	cloudflare__BucketName: 'content'
	cloudflare__FlairsKey: 'flairs'
	cloudflare__HomepageKey: 'homepage'
	cloudflare__KVShortnerNamespaceId: '663cd5c74988404dafbf67e1e06b21e8'
	cloudflare__PreProcessedHomepageKey: 'homepage-ssr'
	cloudflare__SubjectsKey: 'subjects'
	cosmosdb__Container: 'cultpodcasts'
	cosmosdb__DatabaseId: 'cultpodcasts'
	cosmosdb__UseGateWay: 'false'
	delayedYouTubePublication__EvaluationThreshold: '4:00:00'
	indexer__ByPassYouTube: 'false'
	indexer__ReleasedDaysAgo: '2'
	redirects__PodcastBasePath: 'https://cultpodcasts.com/podcast/'
	searchIndex__IndexerName: 'cultpodcasts-indexer'
	searchIndex__IndexName: 'cultpodcasts'
	shortner__ShortnerUrl: 'https://s.cultpodcasts.com'
	subreddit__SubredditName: 'cultpodcasts'
	subreddit__SubredditTitleMaxLength: '300'
	twitter__HashTag: 'Cult'
	twitter__WithEpisodeUrl: 'true'
})

param discoverySettings= union(youTubeKeyUsage, {
	api__Endpoint:	'https://api.cultpodcasts.com'
	auth0client__Audience: 'https://api.cultpodcasts.com/'
	auth0client__Domain: 'cultpodcasts.uk.auth0.com'
	bluesky__HashTag: 'Cult'
	bluesky__Identifier: 'cultpodcasts.com'
	bluesky__WithEpisodeUrl: 'true'
	cloudflare__BucketName: 'content'
	cloudflare__FlairsKey: 'flairs'
	cloudflare__HomepageKey: 'homepage'
	cloudflare__KVShortnerNamespaceId: '663cd5c74988404dafbf67e1e06b21e8'
	cloudflare__PreProcessedHomepageKey: 'homepage-ssr'
	cloudflare__SubjectsKey: 'subjects'
	cosmosdb__Container: 'cultpodcasts'
	cosmosdb__DatabaseId: 'cultpodcasts'
	cosmosdb__UseGateWay: 'false'
	delayedYouTubePublication__EvaluationThreshold: '4:00:00'
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
	listenNotes__RequestDelaySeconds: '2'
	pushSubscriptions__Subject: 'mailto:vapid@cultpodcasts.com'
	searchIndex__IndexerName: 'cultpodcasts-indexer'
	searchIndex__IndexName: 'cultpodcasts'
	shortner__ShortnerUrl: 'https://s.cultpodcasts.com'
	subreddit__SubredditName: 'cultpodcasts'
	subreddit__SubredditTitleMaxLength: '300'
	twitter__HashTag: 'Cult'
	twitter__WithEpisodeUrl: 'true'
})

param indexerSettings= union(youTubeKeyUsage, {
	api__Endpoint:	'https://api.cultpodcasts.com'
	auth0client__Audience: 'https://api.cultpodcasts.com/'
	auth0client__Domain: 'cultpodcasts.uk.auth0.com'
	bluesky__HashTag: 'Cult'
	bluesky__Identifier: 'cultpodcasts.com'
	bluesky__WithEpisodeUrl: 'true'
	bluesky__ReuseSession: 'true'
	cloudflare__BucketName: 'content'
	cloudflare__FlairsKey: 'flairs'
	cloudflare__HomepageKey: 'homepage'
	cloudflare__KVShortnerNamespaceId: '663cd5c74988404dafbf67e1e06b21e8'
	cloudflare__PreProcessedHomepageKey: 'homepage-ssr'
	cloudflare__SubjectsKey: 'subjects'
	cosmosdb__Container: 'cultpodcasts'
	cosmosdb__DatabaseId: 'cultpodcasts'
	cosmosdb__UseGateWay: 'false'
	delayedYouTubePublication__EvaluationThreshold: '4:00:00'
	indexer__ByPassYouTube: false
	indexer__ReleasedDaysAgo: '2'
	poster__ReleasedDaysAgo: '4'
	postingCriteria__minimumDuration: '0:9:00'
	postingCriteria__TweetDays: '2'
	searchIndex__IndexerName: 'cultpodcasts-indexer'
	searchIndex__IndexName: 'cultpodcasts'
	shortner__ShortnerUrl: 'https://s.cultpodcasts.com'
	subreddit__SubredditName: 'cultpodcasts'
	subreddit__SubredditTitleMaxLength: '300'
	twitter__HashTag: 'Cult'
	twitter__WithEpisodeUrl: 'true'
})