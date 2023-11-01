using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;

namespace Indexer;

[DurableTask(nameof(Poster))]
public class Poster : TaskActivity<object, bool>
{
    private readonly IEpisodeProcessor _episodeProcessor;
    private readonly ILogger _logger;
    private readonly PosterOptions _posterOptions;
    private readonly PostingCriteria _postingCriteria;

    public Poster(
        IEpisodeProcessor episodeProcessor,
        IOptions<PosterOptions> posterOptions,
        IOptions<PostingCriteria> postingCriteria,
        ILogger<Poster> logger)
    {
        _episodeProcessor = episodeProcessor;
        _posterOptions = posterOptions.Value;
        _postingCriteria = postingCriteria.Value;
        _logger = logger;
    }

    public override async Task<bool> RunAsync(TaskActivityContext context, object input)
    {
        _logger.LogInformation($"{nameof(Poster)} initiated.");
        _logger.LogInformation(_posterOptions.ToString());
        _logger.LogInformation(_postingCriteria.ToString());
        var baselineDate = DateTimeHelper.DaysAgo(_posterOptions.ReleasedDaysAgo);

        _logger.LogInformation(
            $"{nameof(RunAsync)} Posting with options released-since: '{baselineDate:dd/MM/yyyy HH:mm:ss}''.");

        if (DryRun.IsDryRun)
        {
            return true;
        }

        ProcessResponse result;
        try
        {
            result = await _episodeProcessor.PostEpisodesSinceReleaseDate(baselineDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure executing {nameof(IEpisodeProcessor)}.{nameof(IEpisodeProcessor.PostEpisodesSinceReleaseDate)}.");
            result = ProcessResponse.Fail(ex.Message);
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
        return result.Success;
    }
}