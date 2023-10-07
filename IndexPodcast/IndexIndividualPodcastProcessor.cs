using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;

namespace IndexPodcast;

internal class IndexIndividualPodcastProcessor
{
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastUpdater _podcastUpdater;
    private readonly ILogger<IndexIndividualPodcastProcessor> _logger;

    public IndexIndividualPodcastProcessor(
        IPodcastRepository podcastRepository,
        IPodcastUpdater podcastUpdater,
        ILogger<IndexIndividualPodcastProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _podcastUpdater = podcastUpdater;
        _logger = logger;
    }

    public async Task Run(Guid podcastId, IndexingContext indexingContext)
    {
        var podcast = await _podcastRepository.GetPodcast(podcastId.ToString());
        if (podcast == null)
        {
            _logger.LogError($"No podcast found with id {podcastId}");
            return;
        }

        await _podcastUpdater.Update(podcast, indexingContext);
    }
}