using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public static class PodcastExtensions
{
    public static bool IsDelayedYouTubePublishing(this Podcast podcast, Episode episode)
    {
        if (string.IsNullOrWhiteSpace(podcast.SpotifyId) && podcast.AppleId == null)
        {
            return false;
        }

        if (IsAudioPodcastAwaitingYouTubeRelease(podcast, episode) ||
            IsYouTubePodcastWithDelayedPosting(podcast))
        {
            var youTubePublishingDelay = podcast.YouTubePublishingDelay();
            if (youTubePublishingDelay > TimeSpan.Zero && episode.Length > TimeSpan.Zero)
            {
                youTubePublishingDelay = youTubePublishingDelay.Add(episode.Length);
            }

            var expiry = episode.Release.Add(youTubePublishingDelay);
            if (expiry > DateTime.UtcNow)
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

public static class PodcastV2Extensions
{
    public static bool IsDelayedYouTubePublishing(this Models.V2.Podcast podcast, Models.V2.Episode episode)
    {
        if (string.IsNullOrWhiteSpace(podcast.SpotifyId) && podcast.AppleId == null)
        {
            return false;
        }

        if (IsAudioPodcastAwaitingYouTubeRelease(podcast, episode) ||
            IsYouTubePodcastWithDelayedPosting(podcast))
        {
            var youTubePublishingDelay = podcast.YouTubePublishingDelay();
            if (youTubePublishingDelay > TimeSpan.Zero && episode.Length > TimeSpan.Zero)
            {
                youTubePublishingDelay = youTubePublishingDelay.Add(episode.Length);
            }

            var expiry = episode.Release.Add(youTubePublishingDelay);
            if (expiry > DateTime.UtcNow)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAudioPodcastAwaitingYouTubeRelease(Models.V2.Podcast podcast, Models.V2.Episode episode)
    {
        return episode.Urls.YouTube == null && podcast.YouTubePublishingDelay() > TimeSpan.Zero;
    }

    private static bool IsYouTubePodcastWithDelayedPosting(Models.V2.Podcast podcast)
    {
        return string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
               !podcast.AppleId.HasValue &&
               !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
               podcast.YouTubePublishingDelay() > TimeSpan.Zero;
    }
}