using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Publishers;

namespace Indexer.Triggers;

public class SearchSuggestionsPublishTrigger(
    ISearchSuggestionsPublisher searchSuggestionsPublisher,
    ILogger<SearchSuggestionsPublishTrigger> logger)
{
    /// <summary>
    /// Weekly refresh of the public typeahead match index on R2 (Sunday 07:00 UTC).
    /// </summary>
    [Function("SearchSuggestionsPublish")]
    public async Task Run(
        [TimerTrigger("0 0 7 * * 0"
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

        var success = await searchSuggestionsPublisher.PublishSearchSuggestions(cancellationToken);
        if (success)
        {
            logger.LogInformation("SearchSuggestionsPublish completed successfully.");
        }
        else
        {
            logger.LogError("SearchSuggestionsPublish failed.");
        }
    }
}
