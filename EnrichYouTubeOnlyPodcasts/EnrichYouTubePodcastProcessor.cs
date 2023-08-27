using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;

namespace EnrichYouTubeOnlyPodcasts;

public class EnrichYouTubePodcastProcessor
{
    private readonly IPodcastRepository _podcastRepository;
    private readonly ILogger<EnrichYouTubePodcastProcessor> _logger;

    public EnrichYouTubePodcastProcessor(
        IPodcastRepository podcastRepository,
        ILogger<EnrichYouTubePodcastProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _logger = logger;
    }

    public async Task Run(EnrichYouTubePodcastRequest request)
    {
        var podcasts = await _podcastRepository.GetAll().ToListAsync();
        var podcast = podcasts.Single(x => x.Id == request.PodcastGuid);
        if (podcast.AppleId.HasValue || !string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            throw new InvalidOperationException(
                "Not appropriate to run this app against a podcast with a Spotify or Apple id");
        }
    }
}
