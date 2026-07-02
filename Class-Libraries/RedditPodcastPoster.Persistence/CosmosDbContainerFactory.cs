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
        return GetContainer(cosmosDbSettings.PodcastsContainer, "cosmosdbv2:PodcastsContainer");
    }

    public Container CreateEpisodesContainer()
    {
        return GetContainer(cosmosDbSettings.EpisodesContainer, "cosmosdbv2:EpisodesContainer");
    }

    public Container CreateSubjectsContainer()
    {
        return GetContainer(cosmosDbSettings.SubjectsContainer, "cosmosdbv2:SubjectsContainer");
    }

    public Container CreateActivitiesContainer()
    {
        return GetContainer(cosmosDbSettings.ActivitiesContainer, "cosmosdbv2:ActivitiesContainer");
    }

    public Container CreateDiscoveryContainer()
    {
        return GetContainer(cosmosDbSettings.DiscoveryContainer, "cosmosdbv2:DiscoveryContainer");
    }

    public Container CreateLookUpsContainer()
    {
        return GetContainer(cosmosDbSettings.LookUpsContainer, "cosmosdbv2:LookUpsContainer");
    }

    public Container CreatePushSubscriptionsContainer()
    {
        return GetContainer(cosmosDbSettings.PushSubscriptionsContainer, "cosmosdbv2:PushSubscriptionsContainer");
    }
}