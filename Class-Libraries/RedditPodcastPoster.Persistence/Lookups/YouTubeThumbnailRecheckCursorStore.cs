using RedditPodcastPoster.Models.Thumbnails;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Stores;

namespace RedditPodcastPoster.Persistence.Lookups;

public sealed class YouTubeThumbnailRecheckCursorStore(ILookupRepository lookupRepository)
    : IYouTubeThumbnailRecheckCursorStore
{
    public Task<YouTubeThumbnailRecheckCursor?> GetAsync(CancellationToken cancellationToken = default)
        => lookupRepository.GetYouTubeThumbnailRecheckCursor();

    public Task SaveAsync(YouTubeThumbnailRecheckCursor cursor, CancellationToken cancellationToken = default)
        => lookupRepository.SaveYouTubeThumbnailRecheckCursor(cursor);
}
