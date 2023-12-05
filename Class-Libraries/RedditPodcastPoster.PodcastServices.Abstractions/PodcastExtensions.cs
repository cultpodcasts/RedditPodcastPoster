using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public static class PodcastExtensions
{
    public static bool IsDelayedYouTubePublishing(this Podcast podcast, Episode episode)
    {
        if (IsAudioPodcastAwaitingYouTubeRelease(podcast, episode) ||
            IsYouTubePodcastWithDelayedPosting(podcast))
        {
            if (episode.Release.Add(podcast.YouTubePublishingDelay()) > DateTime.UtcNow)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAudioPodcastAwaitingYouTubeRelease(Podcast podcast, Episode episode)
    {
        return episode.Urls.YouTube == null && podcast.YouTubePublishingDelay() > TimeSpan.Zero;
    }

    private static bool IsYouTubePodcastWithDelayedPosting(Podcast podcast)
    {
        return string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
               !podcast.AppleId.HasValue &&
               !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
               podcast.YouTubePublishingDelay() > TimeSpan.Zero;
    }
}