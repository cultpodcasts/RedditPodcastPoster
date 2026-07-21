using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IPushSubscriptionRepository : IRepository<PushSubscription>
{
    Task Delete(PushSubscription pushSubscription);
}
