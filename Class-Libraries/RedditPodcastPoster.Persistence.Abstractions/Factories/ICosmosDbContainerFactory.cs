using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Abstractions.Factories;

public interface ICosmosDbContainerFactory
{
    Container CreatePodcastsContainer();
    Container CreateEpisodesContainer();
    Container CreateSubjectsContainer();
    Container CreatePeopleContainer();
    Container CreateActivitiesContainer();
    Container CreateDiscoveryContainer();
    Container CreateLookUpsContainer();
    Container CreatePushSubscriptionsContainer();
}