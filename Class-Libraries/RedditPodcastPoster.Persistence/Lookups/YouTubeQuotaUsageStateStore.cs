using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Stores;

namespace RedditPodcastPoster.Persistence.Lookups;

public sealed class YouTubeQuotaUsageStateStore(ILookupRepository lookupRepository)
    : IYouTubeQuotaUsageStateStore
{
    public Task<YouTubeQuotaUsageState?> GetAsync(CancellationToken cancellationToken = default)
        => lookupRepository.GetYouTubeQuotaUsageState();

    public Task SaveAsync(YouTubeQuotaUsageState state, CancellationToken cancellationToken = default)
        => lookupRepository.SaveYouTubeQuotaUsageState(state);
}
