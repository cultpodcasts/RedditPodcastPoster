using Indexer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Episodes;

namespace Poster;

public class Poster
{
    private readonly IEpisodeProcessor _episodeProcessor;
    private readonly ILogger _logger;
    private readonly PosterOptions _posterOptions;

    public Poster(
        IEpisodeProcessor episodeProcessor,
        IOptions<PosterOptions> posterOptions,
        ILogger<Poster> logger)
    {
        _episodeProcessor = episodeProcessor;
        _posterOptions = posterOptions.Value;
        _logger = logger;
    }

    [Function("Poster")]
    public async Task Run([TimerTrigger("10 */4 * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo timerTimer
    )
    {
        _logger.LogInformation(
            $"{nameof(Poster)}.{nameof(Run)} Initiated. Current timer schedule is: {timerTimer.ScheduleStatus.Next:R}");
        _logger.LogInformation(_posterOptions.ToString());
        var baselineDate = DateTimeHelper.DaysAgo(_posterOptions.ReleasedDaysAgo);

        _logger.LogInformation(
            $"{nameof(Run)} Posting with options released-since: '{baselineDate:dd/MM/yyyy HH:mm:ss}''.");

        var result = await _episodeProcessor.PostEpisodesSinceReleaseDate(baselineDate);
        if (!result.Success)
        {
            _logger.LogError($"{nameof(Run)} Failed to process posts. {result}");
        }
        else
        {
            _logger.LogInformation($"{nameof(Run)} Successfully processed posts. {result}");
        }

        _logger.LogInformation($"{nameof(Run)} Completed");
    }
}