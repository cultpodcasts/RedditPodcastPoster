using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;

namespace RedditPodcastPoster.Common;

public class PodcastProcessor : IPodcastProcessor
{
    private readonly IEpisodeProcessor _episodeProcessor;
    private readonly ILogger<PodcastProcessor> _logger;
    private readonly IPodcastsUpdater _podcastsUpdater;

    public PodcastProcessor(
        IPodcastsUpdater podcastsUpdater,
        IEpisodeProcessor episodeProcessor,
        ILogger<PodcastProcessor> logger)
    {
        _podcastsUpdater = podcastsUpdater;
        _episodeProcessor = episodeProcessor;
        _logger = logger;
    }

    public async Task<ProcessResponse> Process(ProcessRequest processRequest)
    {
        if (processRequest.RefreshEpisodes)
        {
            IndexingContext indexingContext =
                new(processRequest.ReleaseBaseline, processRequest.SkipYouTubeUrlResolving);
            var results = await _podcastsUpdater.UpdatePodcasts(indexingContext);
            if (results.Success)
            {
                _logger.LogInformation(results.ToString());
            }
            else
            {
                _logger.LogError(results.ToString());
            }
        }

        if (processRequest.ReleaseBaseline != null)
        {
            return await _episodeProcessor.PostEpisodesSinceReleaseDate(processRequest.ReleaseBaseline.Value);
        }

        return ProcessResponse.Successful("Operation successful.");
    }
}