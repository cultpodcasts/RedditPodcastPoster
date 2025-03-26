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

        var indexerContext = new IndexerContext(indexPassOperationIs, indexIds.PodcastIdBatches);
        for (var pass = 1; pass <= indexPasses; pass++)
        {
            indexerContext = await context.CallIndexerAsync(new IndexerContextWrapper(indexerContext, pass));
            logger.LogInformation("{nameofIndexer} Pass {pass} complete.", nameof(Indexer), pass);
        }

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