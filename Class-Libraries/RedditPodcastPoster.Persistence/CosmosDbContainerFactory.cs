using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbContainerFactory(
    [FromKeyedServices("cosmos")] CosmosClient cosmosClient,
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

    public Container CreateSubjectsContainer()
    {
        return GetContainer(cosmosDbSettings.SubjectsContainer, "cosmosdb:SubjectsContainer");
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

    public Container CreatePushSubscriptionsContainer()
    {
        return GetContainer(cosmosDbSettings.PushSubscriptionsContainer, "cosmosdb:PushSubscriptionsContainer");
    }
}