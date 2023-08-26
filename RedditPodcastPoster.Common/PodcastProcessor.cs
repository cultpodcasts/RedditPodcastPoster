using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;

namespace RedditPodcastPoster.Common;

public class PodcastProcessor : IPodcastProcessor
{
    private readonly IPodcastsUpdater _podcastsUpdater;
    private readonly IEpisodeProcessor _episodeProcessor;
    private readonly ILogger<PodcastProcessor> _logger;

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
            await _podcastsUpdater.UpdatePodcasts(processRequest.ReleasedSince, processRequest.SkipYouTubeUrlResolving);
        }

        if (processRequest.ReleasedSince != null)
        {
            return await _episodeProcessor.PostEpisodesSinceReleaseDate(processRequest.ReleasedSince.Value);
        }

        return ProcessResponse.Successful();
    }
}