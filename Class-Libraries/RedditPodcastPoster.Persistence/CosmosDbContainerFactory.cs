using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbContainerFactory(
    [FromKeyedServices("v2")] CosmosClient v2CosmosClient,
    IOptions<CosmosDbSettings> cosmosDbSettingsV2,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CosmosDbContainerFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICosmosDbContainerFactory
{
    private readonly CosmosDbSettings cosmosDbSettings = cosmosDbSettingsV2.Value;

    private Container GetV2Container(string containerName, string settingName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException($"Configuration '{settingName}' is required.");
        }

        return v2CosmosClient.GetContainer(cosmosDbSettings.DatabaseId, containerName);
    }

    public Container CreatePodcastsContainer()
    {
        return GetV2Container(cosmosDbSettings.PodcastsContainer, "cosmosdbv2:PodcastsContainer");
    }

    public Container CreateEpisodesContainer()
    {
        return GetV2Container(cosmosDbSettings.EpisodesContainer, "cosmosdbv2:EpisodesContainer");
    }

    public Container CreateSubjectsContainer()
    {
        return GetV2Container(cosmosDbSettings.SubjectsContainer, "cosmosdbv2:SubjectsContainer");
    }

    public Container CreateActivitiesContainer()
    {
        return GetV2Container(cosmosDbSettings.ActivitiesContainer, "cosmosdbv2:ActivitiesContainer");
    }

    public Container CreateDiscoveryContainer()
    {
        return GetV2Container(cosmosDbSettings.DiscoveryContainer, "cosmosdbv2:DiscoveryContainer");
    }

    public Container CreateLookUpsContainer()
    {
        return GetV2Container(cosmosDbSettings.LookUpsContainer, "cosmosdbv2:LookUpsContainer");
    }

    public Container CreatePushSubscriptionsContainer()
    {
        return GetV2Container(cosmosDbSettings.PushSubscriptionsContainer, "cosmosdbv2:PushSubscriptionsContainer");
    }
}