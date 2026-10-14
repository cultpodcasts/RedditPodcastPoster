using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Thumbnails;

public class RecentYouTubeThumbnailRechecker : IRecentYouTubeThumbnailRechecker
{
    public Task<YouTubeThumbnailRecheckResult> Recheck(
        IReadOnlyList<PodcastEpisode> recentEpisodes,
        IndexingContext indexingContext,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(YouTubeThumbnailRecheckResult.None);
}
