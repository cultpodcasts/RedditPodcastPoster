using './main.bicep'

param location = 'uksouth'
param suffix = 'infra'
param storageName = 'cultpodcastsstg'
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
param pushSubscriptionsPublicKey= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'PushSubscriptions-PrivateKey')
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


param apiSettings = {
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
	cosmosdb__UseGateWay: 'true'
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
	youtube__Applications__0__Name: 'CultPodcasts'
	youtube__Applications__1__Name: 'CultPodcasts'
	youtube__Applications__2__Name: 'CultPodcasts'
	youtube__Applications__3__Name: 'CultPodcasts'
	youtube__Applications__4__Name: 'CultPodcasts'
	youtube__Applications__5__Name: 'CultPodcasts'


}