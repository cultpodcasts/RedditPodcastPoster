using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.ApiCache;
using RedditPodcastPoster.ContentPublisher.Publishers;

namespace Indexer.Triggers;

public class SearchSuggestionsPublishTrigger(
    ISearchSuggestionsPublisher searchSuggestionsPublisher,
    IApiCachePurgeCommandBuilder apiCachePurgeCommandBuilder,
    ILogger<SearchSuggestionsPublishTrigger> logger)
{
    /// <summary>
    /// Weekly refresh of the public typeahead match index on R2 (Sunday 07:07 UTC).
    /// Failures throw so AppRequests records Success=false and exceptions appear in telemetry.
    /// After a successful publish, the Cloudflare CLI command that purges the Api edge cache is logged
    /// (no purge is performed here; the Api serves the old copy until max-age expiry unless someone runs it).
    /// </summary>
    [Function("SearchSuggestionsPublish")]
    public async Task Run(
        [TimerTrigger("0 7 7 * * 0"
#if DEBUG
            , RunOnStartup = false
#endif
        )]
        TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "SearchSuggestionsPublish initiated. ScheduleStatus.Next: '{Next}'.",
            timerInfo.ScheduleStatus?.Next);

        await searchSuggestionsPublisher.PublishSearchSuggestions(cancellationToken);

        logger.LogInformation("SearchSuggestionsPublish completed successfully.");

        var purge = apiCachePurgeCommandBuilder.Build([ApiCachedContent.SearchSuggestions]);
        if (purge != null)
        {
            logger.LogInformation(
                "SearchSuggestionsPublish Api cache purge command: {PurgeCommand} {VerifyNote}",
                string.Join(" ; ", purge.Commands),
                string.Join(" ", purge.VerifyLines));
        }
    }
}
