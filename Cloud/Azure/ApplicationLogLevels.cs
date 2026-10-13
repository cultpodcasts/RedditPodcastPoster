using Microsoft.Extensions.Logging;

namespace Azure;

/// <summary>
/// Worker-side log filters shared by every function app (Indexer, Discovery, Api).
/// Host/framework at Warning. Function app namespaces at Information.
/// RedditPodcastPoster at Warning during cost review (high-volume pagination logs),
/// except the episode lookup clearing-house, whose one-line-per-lookup outcome
/// ("Catalogue episode lookup: ...") and request-guard timeouts must reach App Insights.
/// Keep each function app's host.json logLevel section in step with these categories.
/// </summary>
public static class ApplicationLogLevels
{
    /// <summary>
    /// Namespace of CatalogueLookupExecutor and CatalogueRequestGuard
    /// (RedditPodcastPoster.EpisodeLookup project).
    /// </summary>
    public const string EpisodeLookupCategory = "RedditPodcastPoster.EpisodeLookup";

    public static void Configure(ILoggingBuilder logging)
    {
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddFilter("Function", LogLevel.Warning);
        logging.AddFilter("Host", LogLevel.Warning);
        logging.AddFilter("Azure", LogLevel.Warning);
        logging.AddFilter("RedditPodcastPoster", LogLevel.Warning);
        logging.AddFilter(EpisodeLookupCategory, LogLevel.Information);
        logging.AddFilter("Indexer", LogLevel.Information);
        logging.AddFilter("Api", LogLevel.Information);
        logging.AddFilter("Discovery", LogLevel.Information);
    }
}
