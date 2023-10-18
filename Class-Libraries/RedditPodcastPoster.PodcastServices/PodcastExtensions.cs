using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices;

public static class PodcastExtensions
{
    public static bool IsDelayedYouTubePublishing(this Podcast podcast, Episode episode)
    {
        if (IsAudioPodcastAwaitingYouTubeRelease(podcast, episode) ||
            IsYouTubePodcastWithDelayedPosting(podcast))
        {
            var timeSpan = TimeSpan.Parse(podcast.YouTubePublishingDelayTimeSpan);
            if (episode.Release.Add(timeSpan) > DateTime.UtcNow)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAudioPodcastAwaitingYouTubeRelease(Podcast podcast, Episode episode)
    {
        return episode.Urls.YouTube == null &&
               !string.IsNullOrWhiteSpace(podcast!.YouTubePublishingDelayTimeSpan);
    }

    private static bool IsYouTubePodcastWithDelayedPosting(Podcast podcast)
    {
        return string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
               !podcast.AppleId.HasValue &&
               !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
               !string.IsNullOrWhiteSpace(podcast!.YouTubePublishingDelayTimeSpan);
    }
}