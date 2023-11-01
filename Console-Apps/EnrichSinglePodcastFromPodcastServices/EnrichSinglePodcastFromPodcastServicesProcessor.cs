using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

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

    public async Task Run(EnrichPodcastRequest request)
    {
        var podcast = await _podcastRepository.GetPodcast(request.PodcastId);
        if (podcast == null)
        {
            throw new ArgumentException($"No podcast with Guid '{podcast}' found");
        }

        IndexingContext indexingContent;
        if (request.ReleasedSince.HasValue)
        {
            indexingContent = new IndexingContext(DateTimeHelper.DaysAgo(request.ReleasedSince.Value));
        }
        else
        {
            indexingContent = new IndexingContext();
        }

        indexingContent.SkipPodcastDiscovery = false;
        var result = await _podcastUpdater.Update(podcast, indexingContent);
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