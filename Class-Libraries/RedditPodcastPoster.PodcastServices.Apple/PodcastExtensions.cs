using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Apple;

public static class PodcastExtensions
{
    public static FindApplePodcastRequest ToFindApplePodcastRequest(this Podcast podcast)
    {
        return new FindApplePodcastRequest(podcast.AppleId, podcast.Name, podcast.Publisher);
    }
}

public static class PodcastV2Extensions
{
    public static FindApplePodcastRequest ToFindApplePodcastRequest(this Models.V2.Podcast podcast)
    {
        return new FindApplePodcastRequest(podcast.AppleId, podcast.Name, podcast.Publisher);
    }
}