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
            IndexOptions indexOptions = new(processRequest.ReleasedSince, processRequest.SkipYouTubeUrlResolving);
            await _podcastsUpdater.UpdatePodcasts(indexOptions);
        }

        if (processRequest.ReleasedSince != null)
        {
            return await _episodeProcessor.PostEpisodesSinceReleaseDate(processRequest.ReleasedSince.Value);
        }

        return ProcessResponse.Successful();
    }
}