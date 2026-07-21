using RedditPodcastPoster.Models.Notifications;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IPushSubscriptionRepository : IRepository<PushSubscription>
{
    Task Delete(PushSubscription pushSubscription);
}
