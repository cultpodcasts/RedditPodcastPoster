using './main.bicep'

param location = 'uksouth'
param suffix = 'infra'
param storageName = 'cultpodcastsstg'
param runtime = 'dotnet-isolated'

param auth0ClientId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Auth0-ClientId')
param auth0ClientSecret= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), readEnvironmentVariable('MANAGEMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KEYVAULT_NAME'), 'Auth0-ClientSecret')

param apiSettings = {
	api__Endpoint:	'https://api.cultpodcasts.com'
	auth0__Audience: 'https://api.cultpodcasts.com/'
	auth0__Domain: 'cultpodcasts.uk.auth0.com'
	auth0__Issuer: 'https://cultpodcasts.uk.auth0.com/'
	auth0client__Audience: 'https://api.cultpodcasts.com/'
}