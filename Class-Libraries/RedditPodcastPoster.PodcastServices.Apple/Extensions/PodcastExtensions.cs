using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Apple.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Extensions;

public static class PodcastExtensions
{
    public static FindApplePodcastRequest ToFindApplePodcastRequest(this Podcast podcast)
    {
        return new FindApplePodcastRequest(podcast.AppleId, podcast.Name, podcast.Publisher);
    }
}