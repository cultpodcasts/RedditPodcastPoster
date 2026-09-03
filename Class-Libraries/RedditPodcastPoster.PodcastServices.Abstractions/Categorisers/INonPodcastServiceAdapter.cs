using System.Linq.Expressions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

/// <summary>
/// One streaming/non-podcast destination (BBC Sounds, Internet Archive, Vimeo, …).
/// New services register another implementation; submit routing does not grow a switch.
/// </summary>
public interface INonPodcastServiceAdapter
{
    NonPodcastService Service { get; }

    /// <summary>URL shapes the submit/categorise pipeline will ingest.</summary>
    bool IsSubmitUrl(Uri url);

    /// <summary>
    /// URL shapes this adapter can extract metadata for.
    /// May be wider than <see cref="IsSubmitUrl"/> (BBC host vs Sounds/iPlayer path).
    /// </summary>
    bool CanExtract(Uri url);

    Expression<Func<Episode, bool>> StoredUrlEquals(Uri url);

    Episode? FindMatchingEpisode(IEnumerable<Episode> episodes, Uri url);

    Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url);
}
