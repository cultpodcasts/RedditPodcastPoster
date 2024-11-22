using './main.bicep'

param location = 'uksouth'
param suffix = 'infra'
param storageName = 'cultpodcastsstg'
param runtime = 'dotnet-isolated'

var auth0ClientId= az.getSecret(readEnvironmentVariable('INPUT_SUBSCRIPTION-ID'), 'MANAGEMENT', 'cultpodcasts-keyvault', 'Auth0-ClientId')

param apiSettings = {
	api__Endpoint:	'https://api.cultpodcasts.com'
	auth0__Audience: 'https://api.cultpodcasts.com/'
	auth0__Domain: 'cultpodcasts.uk.auth0.com'
	auth0__Issuer: 'https://cultpodcasts.uk.auth0.com/'
	auth0client__Audience: 'https://api.cultpodcasts.com/'
	auth0client__ClientId: '${auth0ClientId}'
}