using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class ApplePodcastEnricher : IApplePodcastEnricher
{
    private readonly IAppleItemResolver _appleItemResolver;
    private readonly ILogger<ApplePodcastEnricher> _logger;

    public ApplePodcastEnricher(
        IAppleItemResolver appleItemResolver,
        ILogger<ApplePodcastEnricher> logger)
    {
        _appleItemResolver = appleItemResolver;
        _logger = logger;
    }

    public async Task AddIdAndUrls(Podcast podcast, IEnumerable<Episode> newEpisodes)
    {
        if (podcast.AppleId == null)
        {
            var matchedPodcast = await _appleItemResolver.FindPodcast(podcast);
            if (matchedPodcast != null)
            {
                podcast.AppleId = matchedPodcast?.Id;
            }
        }

        if (podcast.AppleId != null)
        {
            foreach (var podcastEpisode in newEpisodes)
            {
                if (podcastEpisode.AppleId == null)
                {
                    var episode = await _appleItemResolver.FindEpisode(podcast, podcastEpisode);
                    if (episode != null)
                    {
                        podcastEpisode.AppleId = episode?.Id;
                        podcastEpisode.Urls.Apple = episode?.Url;
                    }
                }
            }
        }
    }
}