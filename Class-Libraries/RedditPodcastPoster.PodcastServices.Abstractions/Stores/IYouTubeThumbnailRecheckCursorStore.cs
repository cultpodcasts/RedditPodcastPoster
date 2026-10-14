using RedditPodcastPoster.Models.Thumbnails;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Stores;

/// <summary>Port for the persisted YouTube thumbnail re-check round-robin cursor (Cosmos LookUps).</summary>
public interface IYouTubeThumbnailRecheckCursorStore
{
    Task<YouTubeThumbnailRecheckCursor?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(YouTubeThumbnailRecheckCursor cursor, CancellationToken cancellationToken = default);
}
