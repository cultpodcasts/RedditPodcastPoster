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
    ILogger<Poster> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly PosterOptions _posterOptions = posterOptions.Value;
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation("{class} initiated. task-activity-context-instance-id: '{contextInstanceId}'.",
            nameof(Poster), context.InstanceId);
        logger.LogInformation(indexerContext.ToString());
        logger.LogInformation(_posterOptions.ToString());
        logger.LogInformation(_postingCriteria.ToString());
        var baselineDate = DateTimeExtensions.DaysAgo(_posterOptions.ReleasedDaysAgo);

        logger.LogInformation(
            "{method} Posting with options released-since: '{baselineDate:O}', max-posts: '{posterOptionsMaxPosts}'.",
            nameof(RunAsync), baselineDate, _posterOptions.MaxPosts);

        if (!activityOptionsProvider.RunPoster(out var reason))
        {
            logger.LogInformation("{class} activity disabled. Reason: '{reason}'.", nameof(Poster), reason);
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

        if (!results.Success)
        {
            logger.LogError("{method} Failed to process posts. {results}", nameof(RunAsync), results);
        }
        else
        {
            logger.LogInformation("{method} Successfully processed posts. {results}", nameof(RunAsync), results);
        }

        var result = indexerContext with { Success = results.Success };
        logger.LogInformation("{method} Completed. Result: {result}", nameof(RunAsync), result);
        return result;
    }
}