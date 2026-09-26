using System.Linq.Expressions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

/// <summary>
/// One streaming destination (BBC Sounds, Internet Archive, Vimeo, …).
/// New services register another implementation; submit routing does not grow a switch.
/// </summary>
public interface INonPodcastServiceAdapter
{
    /// <summary>Wire destination for this URL (BBC Sounds vs iPlayer when one adapter spans both).</summary>
    StreamingService ResolveService(Uri url);

    /// <summary>URL shapes the submit/categorise pipeline will ingest.</summary>
    bool IsSubmitUrl(Uri url);

    /// <summary>
    /// URL shapes this adapter can extract metadata for.
    /// May be wider than <see cref="IsSubmitUrl"/> (BBC host vs Sounds/iPlayer path).
    /// </summary>
    bool CanExtract(Uri url);

    Expression<Func<Episode, bool>> StoredUrlEquals(Uri url);

    /// <summary>
    /// Canonical service URL <see cref="StoredUrlEquals"/> compares against.
    /// Submit and membership must use this, not the raw pasted URL.
    /// </summary>
    Uri CanonicalStoredUrl(Uri url);

    Episode? FindMatchingEpisode(IEnumerable<Episode> episodes, Uri url);

    Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url);

    /// <summary>
    /// Extract metadata from already-fetched HTML or trusted JSON (Worker Browser Rendering / prepare extract).
    /// Services without a registered HTML/JSON path throw <see cref="NotSupportedException"/>.
    /// </summary>
    Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url, string html);
}
