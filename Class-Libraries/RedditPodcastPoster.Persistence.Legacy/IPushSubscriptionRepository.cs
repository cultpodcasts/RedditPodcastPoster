using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Legacy;

public interface IPushSubscriptionRepository
{
    Task Save(PushSubscription pushSubscription);
    IAsyncEnumerable<PushSubscription> GetAll();
    Task Delete(PushSubscription pushSubscription);
}
