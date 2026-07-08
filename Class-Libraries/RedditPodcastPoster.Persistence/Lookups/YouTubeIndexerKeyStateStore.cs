using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Persistence.Lookups;

public sealed class YouTubeIndexerKeyStateStore(ILookupRepository lookupRepository)
    : IYouTubeIndexerKeyStateStore
{
    public Task<YouTubeIndexerKeyState?> GetAsync(CancellationToken cancellationToken = default)
        => lookupRepository.GetYouTubeIndexerKeyState();

    public Task SaveAsync(YouTubeIndexerKeyState state, CancellationToken cancellationToken = default)
        => lookupRepository.SaveYouTubeIndexerKeyState(state);
}
