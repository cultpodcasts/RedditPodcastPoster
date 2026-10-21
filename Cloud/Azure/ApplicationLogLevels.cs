using Microsoft.Extensions.Logging;

namespace Azure;

/// <summary>
/// Worker-side log filters shared by every function app (Indexer, Discovery, Api).
/// Host/framework at Warning. Function app namespaces at Information.
/// RedditPodcastPoster at Warning during cost review (high-volume pagination logs),
/// except the episode lookup clearing-house, whose one-line-per-lookup outcome
/// ("Catalogue episode lookup: ...") and request-guard timeouts must reach App Insights, the episode
/// merger, whose tie-break and single-match decisions must too, and the YouTube candidate preference,
/// whose choices between duplicate uploads must be auditable.
/// Keep each function app's host.json logLevel section in step with these categories.
/// </summary>
public static class ApplicationLogLevels
{
    /// <summary>
    /// Namespace of CatalogueLookupExecutor and CatalogueRequestGuard
    /// (RedditPodcastPoster.EpisodeLookup project).
    /// </summary>
    public const string EpisodeLookupCategory = "RedditPodcastPoster.EpisodeLookup";

    /// <summary>
    /// Namespace of EpisodeMerger (RedditPodcastPoster.PodcastServices project). Its Information
    /// "Episode merge tie-broken" line and Warning single-match rejections are low-volume and record why an
    /// incoming episode merged into one stored episode or was added as new.
    /// </summary>
    public const string EpisodeMergingCategory = "RedditPodcastPoster.PodcastServices.Merging";

    /// <summary>
    /// Namespace of the YouTube candidate preference (📱 / still-image penalties). Logs one line per
    /// multi-candidate choice, so replacements of a finder's pick are visible in App Insights.
    /// </summary>
    public const string YouTubeCandidatePreferenceCategory = "RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference";

    public static void Configure(ILoggingBuilder logging)
    {
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddFilter("Function", LogLevel.Warning);
        logging.AddFilter("Host", LogLevel.Warning);
        logging.AddFilter("Azure", LogLevel.Warning);
        logging.AddFilter("RedditPodcastPoster", LogLevel.Warning);
        logging.AddFilter(EpisodeLookupCategory, LogLevel.Information);
        logging.AddFilter(EpisodeMergingCategory, LogLevel.Information);
        logging.AddFilter(YouTubeCandidatePreferenceCategory, LogLevel.Information);
        logging.AddFilter("Indexer", LogLevel.Information);
        logging.AddFilter("Api", LogLevel.Information);
        logging.AddFilter("Discovery", LogLevel.Information);
    }
}
