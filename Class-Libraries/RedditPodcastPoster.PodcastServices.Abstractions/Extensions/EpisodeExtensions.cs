using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

public static class EpisodeV2Extensions
{
    extension(Episode episode)
    {
        public bool HasAccurateReleaseTime()
        {
            return
                episode.Urls.Apple != null &&
                episode.AppleId != null &&
                episode.Release.TimeOfDay != TimeSpan.Zero;
        }
    }
}