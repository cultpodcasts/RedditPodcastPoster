using System.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;

namespace Indexer;

[DurableTask(nameof(Poster))]
public class Poster(
    IEpisodeProcessor episodeProcessor,
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<PosterOptions> posterOptions,
    IOptions<PostingCriteria> postingCriteria,
    IOptions<IndexerOptions> indexerOptions,
    ILogger<Poster> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly PosterOptions _posterOptions = posterOptions.Value;
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        var runStopwatch = Stopwatch.StartNew();

        logger.LogInformation("{class} initiated. task-activity-context-instance-id: '{contextInstanceId}'.",
            nameof(Poster), context.InstanceId);
        logger.LogInformation(indexerContext.ToString());
        logger.LogInformation(_posterOptions.ToString());
        logger.LogInformation(_postingCriteria.ToString());
        var baselineDate = DateTimeExtensions.DaysAgo(_posterOptions.ReleasedDaysAgo);

        logger.LogInformation(
            "{method} Posting with options released-since: '{baselineDate:O}', max-posts: '{posterOptionsMaxPosts}'.",
            nameof(RunAsync), baselineDate, _posterOptions.MaxPosts);

        if (_indexerOptions.EnableCostInstrumentation)
        {
            logger.LogInformation(
                "PosterCostProbe.Start instance-id='{InstanceId}' released-since='{ReleasedSince:O}' max-posts='{MaxPosts}'.",
                context.InstanceId,
                baselineDate,
                _posterOptions.MaxPosts);
        }

        if (!activityOptionsProvider.RunPoster(out var reason))
        {
            logger.LogWarning("{class} activity disabled. Reason: '{reason}'.", nameof(Poster), reason);
            return indexerContext with { Success = true };
        }
        else
        {
            logger.LogInformation("{class} activity enabled. Reason: '{reason}'.", nameof(Poster), reason);
        }

        if (indexerContext.PosterOperationId == null)
        {
            throw new ArgumentNullException(nameof(indexerContext.PosterOperationId));
        }

        ProcessResponse results;
        var processStopwatch = Stopwatch.StartNew();
        try
        {
            results = await episodeProcessor.PostEpisodesSinceReleaseDate(
                baselineDate,
                _posterOptions.MaxPosts,
                indexerContext is { SkipYouTubeUrlResolving: false, YouTubeError: false },
                indexerContext is { SkipSpotifyUrlResolving: false, SpotifyError: false });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure executing {nameofIEpisodeProcessor}.{nameofIEpisodeProcessorPostEpisodesSinceReleaseDate)}.",
                nameof(IEpisodeProcessor), nameof(IEpisodeProcessor.PostEpisodesSinceReleaseDate));
            results = ProcessResponse.Fail(ex.Message);
        }
        processStopwatch.Stop();

        if (!results.Success)
        {
            logger.LogError("{method} Failed to process posts. {results}", nameof(RunAsync), results);
        }
        else
        {
            logger.LogInformation("{method} Successfully processed posts. {results}", nameof(RunAsync), results);
        }

        var result = indexerContext with { Success = results.Success };
        runStopwatch.Stop();

        if (_indexerOptions.EnableCostInstrumentation)
        {
            logger.LogInformation(
                "PosterCostProbe.Complete instance-id='{InstanceId}' success='{Success}' process-ms='{ProcessMs}' total-ms='{TotalMs}'.",
                context.InstanceId,
                result.Success,
                processStopwatch.ElapsedMilliseconds,
                runStopwatch.ElapsedMilliseconds);
        }

        logger.LogInformation("{method} Completed. Result: {result}", nameof(RunAsync), result);
        return result;
    }
}