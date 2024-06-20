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
    IOptions<PosterOptions> posterOptions,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<Poster> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly PosterOptions _posterOptions = posterOptions.Value;
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation(
            $"{nameof(Poster)} initiated. task-activity-context-instance-id: '{context.InstanceId}'.");
        logger.LogInformation(indexerContext.ToString());
        logger.LogInformation(_posterOptions.ToString());
        logger.LogInformation(_postingCriteria.ToString());
        var baselineDate = DateTimeExtensions.DaysAgo(_posterOptions.ReleasedDaysAgo);

        logger.LogInformation(
            $"{nameof(RunAsync)} Posting with options released-since: '{baselineDate:O}''.");

        if (DryRun.IsPosterDryRun)
        {
            return indexerContext with {Success = true};
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
                indexerContext is {SkipYouTubeUrlResolving: false, YouTubeError: false},
                indexerContext is {SkipSpotifyUrlResolving: false, SpotifyError: false});
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure executing {nameof(IEpisodeProcessor)}.{nameof(IEpisodeProcessor.PostEpisodesSinceReleaseDate)}.");
            results = ProcessResponse.Fail(ex.Message);
        }

        if (!results.Success)
        {
            logger.LogError($"{nameof(RunAsync)} Failed to process posts. {results}");
        }
        else
        {
            logger.LogInformation($"{nameof(RunAsync)} Successfully processed posts. {results}");
        }

        var result = indexerContext with {Success = results.Success};
        logger.LogInformation($"{nameof(RunAsync)} Completed. Result: {result}");
        return result;
    }
}