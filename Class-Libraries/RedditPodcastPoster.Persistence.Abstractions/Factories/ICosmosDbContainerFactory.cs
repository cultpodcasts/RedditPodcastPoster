using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Abstractions.Factories;

public interface ICosmosDbContainerFactory
{
    Container CreatePodcastsContainer();
    Container CreateEpisodesContainer();
    Container CreateTvShowsContainer();
    Container CreateTvShowEpisodesContainer();
    Container CreateFilmsContainer();
    Container CreateNewsOrganisationsContainer();
    Container CreateNewsReportsContainer();
    Container CreateSubjectsContainer();
    Container CreatePeopleContainer();
    Container CreateActivitiesContainer();
    Container CreateDiscoveryContainer();
    Container CreateLookUpsContainer();
    Container CreateTitleCasingRulesContainer();
    Container CreatePushSubscriptionsContainer();
}
