using './main.bicep'

param location = 'uksouth'
param suffix = 'infra'
param storageName = 'cultpodcastsstg'
param runtime = 'dotnet-isolated'

param apiSettings = {
	api__Endpoint:	'https://api.cultpodcasts.com'
	auth0__Audience: 'https://api.cultpodcasts.com/'
	auth0__Domain: 'cultpodcasts.uk.auth0.com'
	auth0__Issuer: 'https://cultpodcasts.uk.auth0.com/'
	auth0client__Audience: 'https://api.cultpodcasts.com/'
	auth0client__ClientId: az.getSecret(readEnvironmentVariable('AZURE_Subscription_ID'), readEnvironmentVariable('DEPLOYMENT_RESOURCEGROUP_NAME'), readEnvironmentVariable('AZURE_KeyVault_Name'), 'Auth0-ClientId')
}