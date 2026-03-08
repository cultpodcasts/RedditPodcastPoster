using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbContainerFactory(
    [FromKeyedServices("v1")] CosmosClient cosmosClient,
    [FromKeyedServices("v2")] CosmosClient v2CosmosClient,
    IOptions<CosmosDbSettings> cosmosDbSettings,
    IOptions<CosmosDbSettingsV2> cosmosDbSettingsV2,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CosmosDbContainerFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICosmosDbContainerFactory
{
    private readonly CosmosDbSettings _cosmosDbSettings = cosmosDbSettings.Value;
    private readonly CosmosDbSettingsV2 _cosmosDbSettingsV2 = cosmosDbSettingsV2.Value;

    public Container Create()
    {
        return cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
    }

    public Container CreatePodcastsContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettingsV2.PodcastsContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdbv2:PodcastsContainer' is required.");
        }

        return v2CosmosClient.GetContainer(_cosmosDbSettingsV2.DatabaseId, _cosmosDbSettingsV2.PodcastsContainer);
    }

    public Container CreateEpisodesContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettingsV2.EpisodesContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdbv2:EpisodesContainer' is required.");
        }

        return v2CosmosClient.GetContainer(_cosmosDbSettingsV2.DatabaseId, _cosmosDbSettingsV2.EpisodesContainer);
    }

    public Container CreateSubjectsContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettingsV2.SubjectsContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdbv2:SubjectsContainer' is required.");
        }

        return v2CosmosClient.GetContainer(_cosmosDbSettingsV2.DatabaseId, _cosmosDbSettingsV2.SubjectsContainer);
    }

    public Container CreateActivitiesContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettingsV2.ActivitiesContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdbv2:ActivitiesContainer' is required.");
        }

        return v2CosmosClient.GetContainer(_cosmosDbSettingsV2.DatabaseId, _cosmosDbSettingsV2.ActivitiesContainer);
    }

    public Container CreateDiscoveryContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettingsV2.DiscoveryContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdbv2:DiscoveryContainer' is required.");
        }

        return v2CosmosClient.GetContainer(_cosmosDbSettingsV2.DatabaseId, _cosmosDbSettingsV2.DiscoveryContainer);
    }

    public Container CreateLookUpsContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettingsV2.LookUpsContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdbv2:LookUpsContainer' is required.");
        }

        return v2CosmosClient.GetContainer(_cosmosDbSettingsV2.DatabaseId, _cosmosDbSettingsV2.LookUpsContainer);
    }

    public Container CreatePushSubscriptionsContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettingsV2.PushSubscriptionsContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdbv2:PushSubscriptionsContainer' is required.");
        }

        return v2CosmosClient.GetContainer(_cosmosDbSettingsV2.DatabaseId, _cosmosDbSettingsV2.PushSubscriptionsContainer);
    }
}