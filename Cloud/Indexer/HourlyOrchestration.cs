using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(HourlyOrchestration))]
public class HourlyOrchestration : TaskOrchestrator<object, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskOrchestrationContext context, object input)
    {
        var logger = context.CreateReplaySafeLogger<HourlyOrchestration>();
        logger.LogInformation(
            "{nameofHourlyOrchestration}.{nameofRunAsync} initiated. Instance-id: '{contextInstanceId}'.",
            nameof(HourlyOrchestration), nameof(RunAsync), context.InstanceId);

        const int indexPasses = 4;
        var indexPassOperationIs = Enumerable.Range(1, indexPasses).Select(_ => context.NewGuid()).ToArray();

        var indexIds = await context.CallIndexIdProviderAsync(new IndexIdProviderRequest(indexPasses));
        logger.LogInformation("{nameofIndexIdProvider} complete.", nameof(IndexIdProvider));

        var hourUtc = context.CurrentUtcDateTime.Hour;
        var (firstPass, lastPass) = HourlyIndexingPassSelector.SelectPasses(hourUtc, indexPasses);
        var youtubeEnabledHour = hourUtc % 6 == 0;
        logger.LogWarning(
            "HourlyOrchestration pass-selection hour-utc='{HourUtc}' first-pass='{FirstPass}' last-pass='{LastPass}' youtube-enabled-hour='{YouTubeEnabledHour}' orchestration-instance-id='{OrchestrationInstanceId}'",
            hourUtc, firstPass, lastPass, youtubeEnabledHour, context.InstanceId);
        logger.LogWarning(
            "HourlyOrchestration indexer-operation-ids pass-1='{Pass1OperationId}' pass-2='{Pass2OperationId}' pass-3='{Pass3OperationId}' pass-4='{Pass4OperationId}'",
            indexPassOperationIs[0], indexPassOperationIs[1], indexPassOperationIs[2], indexPassOperationIs[3]);

        var indexerContext = new IndexerContext(indexPassOperationIs, indexIds.PodcastIdBatches);
        for (var pass = firstPass; pass <= lastPass; pass++)
        {
            indexerContext = await context.CallIndexerAsync(new IndexerContextWrapper(indexerContext, pass));
            var batchPodcastCount = indexerContext.IndexIds?[pass - 1]?.Length ?? 0;
            var operationId = indexerContext.IndexerPassOperationIds?[pass - 1];
            logger.LogWarning(
                "HourlyOrchestration indexer-pass-complete pass='{Pass}' hour-utc='{HourUtc}' operation-id='{OperationId}' podcast-count='{PodcastCount}' success='{Success}' skip-youtube='{SkipYouTube}' youtube-error='{YouTubeError}'",
                pass, hourUtc, operationId, batchPodcastCount, indexerContext.Success,
                indexerContext.SkipYouTubeUrlResolving, indexerContext.YouTubeError);

            if (pass == 4)
            {
                logger.LogWarning(
                    "HourlyOrchestration batch-4-rollup hour-utc='{HourUtc}' operation-id='{OperationId}' podcast-count='{PodcastCount}' success='{Success}' skip-youtube='{SkipYouTube}' youtube-error='{YouTubeError}'",
                    hourUtc, operationId, batchPodcastCount, indexerContext.Success,
                    indexerContext.SkipYouTubeUrlResolving, indexerContext.YouTubeError);
            }

            logger.LogInformation("{nameofIndexer} complete - Pass {pass}.", nameof(Indexer), pass);
        }

        indexerContext = await context.CallLoadRecentCandidatesAsync(indexerContext);
        logger.LogInformation("{nameofLoadRecentCandidates} complete.", nameof(LoadRecentCandidates));

        indexerContext =
            await context.CallCategoriserAsync(indexerContext with {CategoriserOperationId = context.NewGuid()});
        logger.LogInformation("{nameofCategoriser} complete.", nameof(Categoriser));

        indexerContext = await context.CallPosterAsync(indexerContext with {PosterOperationId = context.NewGuid()});
        logger.LogInformation("{nameofPoster} complete.", nameof(Poster));

        indexerContext =
            await context.CallPublisherAsync(indexerContext with {PublisherOperationId = context.NewGuid()});
        logger.LogInformation("{nameofPublisher} complete.", nameof(Publisher));

        indexerContext = await context.CallTweetAsync(indexerContext with {TweetOperationId = context.NewGuid()});
        logger.LogInformation("{nameofTweet} complete.", nameof(Tweet));

        indexerContext = await context.CallBlueskyAsync(indexerContext with {BlueskyOperationId = context.NewGuid()});
        logger.LogInformation("{nameofBluesky} complete. All tasks complete.", nameof(Bluesky));

        return indexerContext;
    }
}