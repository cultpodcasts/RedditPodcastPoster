using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

public static class EpisodeExtensions
{
    public static bool HasAccurateReleaseTime(this Episode episode)
    {
        return
            episode.Urls.Apple != null &&
            episode.AppleId != null &&
            episode.Release.TimeOfDay != TimeSpan.Zero;
    }
}