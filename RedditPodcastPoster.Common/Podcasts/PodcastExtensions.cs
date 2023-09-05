using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public static class PodcastExtensions
{
    public static bool IsDelayedYouTubePublishing(this Podcast podcast, Episode episode)
    {
        if (episode.Urls.YouTube == null &&
            !string.IsNullOrWhiteSpace(podcast!.YouTubePublishingDelayTimeSpan))
        {
            var timeSpan = TimeSpan.Parse(podcast.YouTubePublishingDelayTimeSpan);
            if (episode.Release.Add(timeSpan) > DateTime.UtcNow)
            {
                return true;
            }
        }

        return false;
    }
}