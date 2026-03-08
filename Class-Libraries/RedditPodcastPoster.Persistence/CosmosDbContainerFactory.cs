using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbContainerFactory(
    CosmosClient cosmosClient,
    IOptions<CosmosDbSettings> cosmosDbSettings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CosmosDbContainerFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICosmosDbContainerFactory
{
    private readonly CosmosDbSettings _cosmosDbSettings = cosmosDbSettings.Value;

    public Container Create()
    {
        return cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);
    }

    public Container CreatePodcastsContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettings.PodcastsContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdb:PodcastsContainer' is required.");
        }

        return cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.PodcastsContainer);
    }

    public Container CreateEpisodesContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettings.EpisodesContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdb:EpisodesContainer' is required.");
        }

        return cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.EpisodesContainer);
    }

    public Container CreateSubjectsContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettings.SubjectsContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdb:SubjectsContainer' is required.");
        }

        return cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.SubjectsContainer);
    }

    public Container CreateActivitiesContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettings.ActivitiesContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdb:ActivitiesContainer' is required.");
        }

        return cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.ActivitiesContainer);
    }

    public Container CreateDiscoveryContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettings.DiscoveryContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdb:DiscoveryContainer' is required.");
        }

        return cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.DiscoveryContainer);
    }

    public Container CreateLookUpsContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettings.LookUpsContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdb:LookUpsContainer' is required.");
        }

        return cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.LookUpsContainer);
    }

    public Container CreatePushSubscriptionsContainer()
    {
        if (string.IsNullOrWhiteSpace(_cosmosDbSettings.PushSubscriptionsContainer))
        {
            throw new InvalidOperationException("Configuration 'cosmosdb:PushSubscriptionsContainer' is required.");
        }

        return cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.PushSubscriptionsContainer);
    }
}