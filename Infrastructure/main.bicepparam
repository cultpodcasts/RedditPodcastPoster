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


}