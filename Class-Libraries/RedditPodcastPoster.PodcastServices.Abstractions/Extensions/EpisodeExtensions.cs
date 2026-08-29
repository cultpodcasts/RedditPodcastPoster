using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

public static class EpisodeExtensions
{
    extension(Episode episode)
    {
        public bool HasAccurateReleaseTime()
        {
            return
                EpisodeServicePresence.HasUrl(episode, ServiceKeys.Apple) &&
                EpisodeServicePresence.AppleEpisodeId(episode) != null &&
                episode.Release.TimeOfDay != TimeSpan.Zero;
        }
    }
}