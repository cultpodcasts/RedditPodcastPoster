using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IPushSubscriptionRepository : IRepository<PushSubscription>
{
    Task Delete(PushSubscription pushSubscription);
}
