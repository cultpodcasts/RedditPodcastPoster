using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Thumbnails;

/// <summary>
/// Re-checks the stored YouTube image of recently released episodes. YouTube can serve a real
/// <c>maxresdefault</c> / <c>hq720</c> frame at index time and withdraw it later (404 with a 120x90
/// grey placeholder). A withdrawn image is re-resolved and replaced; an image is never cleared.
/// </summary>
public interface IRecentYouTubeThumbnailRechecker
{
    Task<YouTubeThumbnailRecheckResult> Recheck(
        IReadOnlyList<PodcastEpisode> recentEpisodes,
        IndexingContext indexingContext,
        CancellationToken cancellationToken = default);
}
