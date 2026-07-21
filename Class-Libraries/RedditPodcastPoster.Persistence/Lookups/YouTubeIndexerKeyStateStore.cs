using RedditPodcastPoster.Models.YouTubeQuota;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Stores;

namespace RedditPodcastPoster.Persistence.Lookups;

public sealed class YouTubeIndexerKeyStateStore(ILookupRepository lookupRepository)
    : IYouTubeIndexerKeyStateStore
{
    public Task<YouTubeIndexerKeyState?> GetAsync(CancellationToken cancellationToken = default)
        => lookupRepository.GetYouTubeIndexerKeyState();

    public Task SaveAsync(YouTubeIndexerKeyState state, CancellationToken cancellationToken = default)
        => lookupRepository.SaveYouTubeIndexerKeyState(state);
}
