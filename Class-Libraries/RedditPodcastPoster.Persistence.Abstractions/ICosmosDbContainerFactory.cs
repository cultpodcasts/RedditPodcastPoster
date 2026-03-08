using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ICosmosDbContainerFactory
{
    Container Create();
    Container CreatePodcastsContainer();
    Container CreateEpisodesContainer();
    Container CreateSubjectsContainer();
    Container CreateActivitiesContainer();
    Container CreateDiscoveryContainer();
    Container CreateLookupContainer();
    Container CreatePushSubscriptionsContainer();
}