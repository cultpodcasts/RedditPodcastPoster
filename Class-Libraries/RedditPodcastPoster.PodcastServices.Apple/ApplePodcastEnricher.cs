using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class ApplePodcastEnricher : IApplePodcastEnricher
{
    private readonly IApplePodcastResolver _applePodcastResolver;
    private readonly ILogger<ApplePodcastEnricher> _logger;

    public ApplePodcastEnricher(
        IApplePodcastResolver applePodcastResolver,
        ILogger<ApplePodcastEnricher> logger)
    {
        _applePodcastResolver = applePodcastResolver;
        _logger = logger;
    }

    public async Task AddId(Podcast podcast)
    {
        if (podcast.AppleId == null)
        {
            var matchedPodcast = await _applePodcastResolver.FindPodcast(PodcastExtensions.ToFindApplePodcastRequest(podcast));
            if (matchedPodcast != null)
            {
                podcast.AppleId = matchedPodcast?.Id;
            }
        }
    }
}