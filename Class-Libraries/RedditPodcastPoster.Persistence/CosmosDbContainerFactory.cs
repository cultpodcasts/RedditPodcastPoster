using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbContainerFactory(
    [FromKeyedServices("v2")] CosmosClient v2CosmosClient,
    IOptions<CosmosDbSettingsV2> cosmosDbSettingsV2,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CosmosDbContainerFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICosmosDbContainerFactory
{
    private readonly CosmosDbSettingsV2 _cosmosDbSettingsV2 = cosmosDbSettingsV2.Value;

    private Container GetV2Container(string containerName, string settingName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException($"Configuration '{settingName}' is required.");
        }

        return v2CosmosClient.GetContainer(_cosmosDbSettingsV2.DatabaseId, containerName);
    }

    public Container CreatePodcastsContainer()
    {
        return GetV2Container(_cosmosDbSettingsV2.PodcastsContainer, "cosmosdbv2:PodcastsContainer");
    }

    public Container CreateEpisodesContainer()
    {
        return GetV2Container(_cosmosDbSettingsV2.EpisodesContainer, "cosmosdbv2:EpisodesContainer");
    }

    public Container CreateSubjectsContainer()
    {
        return GetV2Container(_cosmosDbSettingsV2.SubjectsContainer, "cosmosdbv2:SubjectsContainer");
    }

    public Container CreateActivitiesContainer()
    {
        return GetV2Container(_cosmosDbSettingsV2.ActivitiesContainer, "cosmosdbv2:ActivitiesContainer");
    }

    public Container CreateDiscoveryContainer()
    {
        return GetV2Container(_cosmosDbSettingsV2.DiscoveryContainer, "cosmosdbv2:DiscoveryContainer");
    }

    public Container CreateLookUpsContainer()
    {
        return GetV2Container(_cosmosDbSettingsV2.LookUpsContainer, "cosmosdbv2:LookUpsContainer");
    }

    public Container CreatePushSubscriptionsContainer()
    {
        return GetV2Container(_cosmosDbSettingsV2.PushSubscriptionsContainer, "cosmosdbv2:PushSubscriptionsContainer");
    }
}