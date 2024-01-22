using Azure;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;

namespace Indexer;

[DurableTask(nameof(Poster))]
public class Poster(
    IEpisodeProcessor episodeProcessor,
    IActivityMarshaller activityMarshaller,
    IOptions<PosterOptions> posterOptions,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<Poster> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly ILogger _logger = logger;
    private readonly PosterOptions _posterOptions = posterOptions.Value;
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        _logger.LogInformation(
            $"{nameof(Poster)} initiated. Instance-id: '{context.InstanceId}', Poster-Operation-Id: '{indexerContext.PosterOperationId}'.");
        _logger.LogInformation(_posterOptions.ToString());
        _logger.LogInformation(_postingCriteria.ToString());
        var baselineDate = DateTimeHelper.DaysAgo(_posterOptions.ReleasedDaysAgo);

        _logger.LogInformation(
            $"{nameof(RunAsync)} Posting with options released-since: '{baselineDate:dd/MM/yyyy HH:mm:ss}''.");

        if (DryRun.IsPosterDryRun)
        {
            return indexerContext with {Success = true};
        }

        if (indexerContext.PosterOperationId == null)
        {
            throw new ArgumentNullException(nameof(indexerContext.PosterOperationId));
        }

        var activityBooked = await activityMarshaller.Initiate(indexerContext.PosterOperationId.Value, nameof(Poster));
        if (activityBooked != ActivityStatus.Initiated)
        {
            return indexerContext with
            {
                DuplicatePosterOperation = true
            };
        }

        ProcessResponse result;
        try
        {
            result = await episodeProcessor.PostEpisodesSinceReleaseDate(
                baselineDate,
                indexerContext is {SkipYouTubeUrlResolving: false, YouTubeError: false},
                indexerContext is {SkipSpotifyUrlResolving: false, SpotifyError: false});
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure executing {nameof(IEpisodeProcessor)}.{nameof(IEpisodeProcessor.PostEpisodesSinceReleaseDate)}.");
            result = ProcessResponse.Fail(ex.Message);
        }
        finally
        {
            try
            {
                activityBooked =
                    await activityMarshaller.Complete(indexerContext.PosterOperationId.Value, nameof(Poster));
                if (activityBooked != ActivityStatus.Completed)
                {
                    _logger.LogError("Failure to complete activity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure to complete activity.");
            }
        }

        if (!result.Success)
        {
            _logger.LogError($"{nameof(RunAsync)} Failed to process posts. {result}");
        }
        else
        {
            _logger.LogInformation($"{nameof(RunAsync)} Successfully processed posts. {result}");
        }

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with {Success = result.Success};
    }
}