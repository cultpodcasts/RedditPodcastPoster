using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IPushSubscriptionRepositoryV2 : IRepository<PushSubscription>
{
    Task Delete(PushSubscription pushSubscription);
}
