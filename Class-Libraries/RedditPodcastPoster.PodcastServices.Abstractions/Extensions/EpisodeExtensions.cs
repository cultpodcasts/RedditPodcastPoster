using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

public static class EpisodeExtensions
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