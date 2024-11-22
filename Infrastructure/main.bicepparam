using './main.bicep'

param location = 'uksouth'
param suffix = 'infra'
param storageName = 'cultpodcastsstg'
param runtime = 'dotnet-isolated'

param apiSettings object = {
	NewVal:	'Phoey'
}