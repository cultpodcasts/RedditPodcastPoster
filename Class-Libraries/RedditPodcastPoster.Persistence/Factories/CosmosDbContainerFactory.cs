using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
using RedditPodcastPoster.Persistence.Configuration;

namespace RedditPodcastPoster.Persistence.Factories;

public class CosmosDbContainerFactory(
    CosmosClient cosmosClient,
    IOptions<CosmosDbSettings> cosmosDbSettingsOptions,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CosmosDbContainerFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICosmosDbContainerFactory
{
    private readonly CosmosDbSettings cosmosDbSettings = cosmosDbSettingsOptions.Value;

    private Container GetContainer(string containerName, string settingName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException($"Configuration '{settingName}' is required.");
        }

        return cosmosClient.GetContainer(cosmosDbSettings.DatabaseId, containerName);
    }

    public Container CreatePodcastsContainer()
    {
        return GetContainer(cosmosDbSettings.PodcastsContainer, "cosmosdb:PodcastsContainer");
    }

    public Container CreateEpisodesContainer()
    {
        return GetContainer(cosmosDbSettings.EpisodesContainer, "cosmosdb:EpisodesContainer");
    }

    public Container CreateTvShowsContainer()
    {
        return GetContainer(cosmosDbSettings.TvShowsContainer, "cosmosdb:TvShowsContainer");
    }

    public Container CreateTvShowEpisodesContainer()
    {
        return GetContainer(cosmosDbSettings.TvShowEpisodesContainer, "cosmosdb:TvShowEpisodesContainer");
    }

    public Container CreateFilmsContainer()
    {
        return GetContainer(cosmosDbSettings.FilmsContainer, "cosmosdb:FilmsContainer");
    }

    public Container CreateNewsOrganisationsContainer()
    {
        return GetContainer(cosmosDbSettings.NewsOrganisationsContainer, "cosmosdb:NewsOrganisationsContainer");
    }

    public Container CreateNewsReportsContainer()
    {
        return GetContainer(cosmosDbSettings.NewsReportsContainer, "cosmosdb:NewsReportsContainer");
    }

    public Container CreateSubjectsContainer()
    {
        return GetContainer(cosmosDbSettings.SubjectsContainer, "cosmosdb:SubjectsContainer");
    }

    public Container CreatePeopleContainer()
    {
        return GetContainer(cosmosDbSettings.PeopleContainer, "cosmosdb:PeopleContainer");
    }

    public Container CreateActivitiesContainer()
    {
        return GetContainer(cosmosDbSettings.ActivitiesContainer, "cosmosdb:ActivitiesContainer");
    }

    public Container CreateDiscoveryContainer()
    {
        return GetContainer(cosmosDbSettings.DiscoveryContainer, "cosmosdb:DiscoveryContainer");
    }

    public Container CreateLookUpsContainer()
    {
        return GetContainer(cosmosDbSettings.LookUpsContainer, "cosmosdb:LookUpsContainer");
    }

    public Container CreateTitleCasingRulesContainer()
    {
        return GetContainer(cosmosDbSettings.TitleCasingRulesContainer, "cosmosdb:TitleCasingRulesContainer");
    }

    public Container CreatePushSubscriptionsContainer()
    {
        return GetContainer(cosmosDbSettings.PushSubscriptionsContainer, "cosmosdb:PushSubscriptionsContainer");
    }
}
