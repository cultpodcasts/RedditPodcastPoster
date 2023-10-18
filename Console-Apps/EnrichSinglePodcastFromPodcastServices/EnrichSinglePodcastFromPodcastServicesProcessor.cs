using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Persistence;

namespace EnrichSinglePodcastFromPodcastServices;

public class EnrichSinglePodcastFromPodcastServicesProcessor
{
    private readonly ILogger<EnrichSinglePodcastFromPodcastServicesProcessor> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastUpdater _podcastUpdater;

    public EnrichSinglePodcastFromPodcastServicesProcessor(
        IPodcastUpdater podcastUpdater,
        IPodcastRepository podcastRepository,
        ILogger<EnrichSinglePodcastFromPodcastServicesProcessor> logger)
    {
        _podcastUpdater = podcastUpdater;
        _podcastRepository = podcastRepository;
        _logger = logger;
    }

    public async Task Run(Guid podcastId)
    {
        var podcast = await _podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            throw new ArgumentException($"No podcast with Guid '{podcast}' found");
        }

        var result = await _podcastUpdater.Update(podcast, new IndexingContext() {SkipPodcastDiscovery = false});
        if (!result.Success)
        {
            _logger.LogError(result.ToString());
        }
        else
        {
            _logger.LogInformation(result.ToString());
        }
    }
}