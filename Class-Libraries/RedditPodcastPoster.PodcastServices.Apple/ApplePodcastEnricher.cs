using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class ApplePodcastEnricher(
    IApplePodcastResolver applePodcastResolver,
    ILogger<ApplePodcastEnricher> logger)
    : IApplePodcastEnricher
{
    public async Task AddId(Podcast podcast)
    {
        if (podcast.AppleId == null)
        {
            var matchedPodcast = await applePodcastResolver.FindPodcast(PodcastExtensions.ToFindApplePodcastRequest(podcast));
            if (matchedPodcast != null)
            {
                podcast.AppleId = matchedPodcast?.Id;
            }
        }
    }
}