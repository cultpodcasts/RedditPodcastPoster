using Api.Models;
using RedditPodcastPoster.Models.Cosmos;

namespace Api.Services.PushSubscriptions;

public interface IPushSubscriptionCreateService
{
    Task<PushSubscriptionCreateResult> CreateAsync(
        PushSubscription pushSubscription,
        string? subject,
        CancellationToken cancellationToken);
}
