using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IPushSubscriptionRepositoryV2
{
    Task Save(PushSubscription pushSubscription);
    IAsyncEnumerable<PushSubscription> GetAll();
    Task Delete(PushSubscription pushSubscription);
}
