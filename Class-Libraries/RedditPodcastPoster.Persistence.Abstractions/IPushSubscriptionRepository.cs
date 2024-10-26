using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IPushSubscriptionRepository
{
    Task Save(PushSubscription pushSubscription);
    IAsyncEnumerable<PushSubscription> GetAll();
    Task Delete(PushSubscription pushSubscription);
}