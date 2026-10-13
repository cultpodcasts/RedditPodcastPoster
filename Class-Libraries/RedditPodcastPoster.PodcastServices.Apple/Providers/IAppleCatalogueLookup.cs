using RedditPodcastPoster.PodcastServices.Apple.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

/// <summary>
/// Quick iTunes catalogue calls (itunes.apple.com, public, no token). Both are single fast requests behind the
/// timeout guard; neither ever pages.
/// </summary>
public interface IAppleCatalogueLookup
{
    /// <summary>Show size from <c>/lookup?id=…</c> <c>trackCount</c>, cached per pass. Null when unavailable.</summary>
    Task<int?> GetEpisodeCount(long podcastId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>/search?term=…&amp;entity=podcastEpisode</c>, keeping only episodes of <paramref name="podcastId"/>
    /// (iTunes <c>collectionId</c>). Episode ids are the same ids amp-api uses.
    /// </summary>
    Task<IReadOnlyList<AppleEpisode>> SearchShowEpisodes(long podcastId, string title, CancellationToken cancellationToken = default);
}
