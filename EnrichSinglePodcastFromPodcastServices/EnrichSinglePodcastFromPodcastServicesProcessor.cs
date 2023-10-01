using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;

namespace EnrichSinglePodcastFromPodcastServices;

public class EnrichSinglePodcastFromPodcastServicesProcessor
{
    private readonly IPodcastUpdater _podcastUpdater;
    private readonly IPodcastRepository _podcastRepository;
    private readonly ILogger<EnrichSinglePodcastFromPodcastServicesProcessor> _logger;

    public EnrichSinglePodcastFromPodcastServicesProcessor(
        IPodcastUpdater podcastUpdater,
        IPodcastRepository podcastRepository,
        ILogger<EnrichSinglePodcastFromPodcastServicesProcessor> logger)
    {
        _podcastUpdater = podcastUpdater;
        _podcastRepository = podcastRepository;
        _logger = logger;
    }

    public async Task Run(string podcastId)
    {
        var podcast = await _podcastRepository.GetPodcast(podcastId.ToString());
        if (podcast == null)
        {
            throw new ArgumentException($"No podcast with Guid '{podcast}' found");
        }

        await _podcastUpdater.Update(podcast, new IndexingContext(null, false));
        await _podcastRepository.Update(podcast);
    }
}