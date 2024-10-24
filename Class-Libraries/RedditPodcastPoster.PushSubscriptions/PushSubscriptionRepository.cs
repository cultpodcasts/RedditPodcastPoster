using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.PushSubscriptions;

public class PushSubscriptionRepository(
    IDataRepository repository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PushSubscriptionRepository> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IPushSubscriptionRepository
{
    public Task Save(PushSubscription pushSubscription)
    {
        return repository.Write(pushSubscription);
    }

    public IAsyncEnumerable<PushSubscription> GetAll()
    {
        return repository.GetAll<PushSubscription>();
    }
}