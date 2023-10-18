using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace IndexPodcast;

internal class IndexIndividualPodcastProcessor
{
    private readonly ILogger<IndexIndividualPodcastProcessor> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastUpdater _podcastUpdater;

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
        var podcast = await _podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            _logger.LogError($"No podcast found with id {podcastId}");
            return;
        }

        await _podcastUpdater.Update(podcast, indexingContext);
    }
}