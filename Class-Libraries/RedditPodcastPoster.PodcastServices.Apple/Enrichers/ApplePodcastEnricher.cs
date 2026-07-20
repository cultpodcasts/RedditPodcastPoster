using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;

namespace RedditPodcastPoster.PodcastServices.Apple.Enrichers;

public class ApplePodcastEnricher(
    IApplePodcastResolver applePodcastResolver,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<ApplePodcastEnricher> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IApplePodcastEnricher
{
    public async Task AddId(Podcast podcast)
    {
        if (podcast.AppleId == null)
        {
            var matchedPodcast = await applePodcastResolver.FindPodcast(podcast.ToFindApplePodcastRequest());
            if (matchedPodcast != null)
            {
                podcast.AppleId = matchedPodcast?.Id;
            }
        }
    }
}