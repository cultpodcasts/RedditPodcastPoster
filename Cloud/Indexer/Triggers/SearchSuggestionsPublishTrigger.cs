using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Publishers;

namespace Indexer.Triggers;

public class SearchSuggestionsPublishTrigger(
    ISearchSuggestionsPublisher searchSuggestionsPublisher,
    ILogger<SearchSuggestionsPublishTrigger> logger)
{
    /// <summary>
    /// Weekly refresh of the public typeahead match index on R2 (Sunday 07:07 UTC).
    /// Failures throw so AppRequests records Success=false and exceptions appear in telemetry.
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
    }
}
