using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public static class PodcastExtensions
{
    public static bool IsDelayedYouTubePublishing(this Podcast podcast, Episode episode)
    {
        if (!HasAudioPlatformConfigured(podcast))
        {
            return false;
        }

        if (!IsAudioPodcastAwaitingYouTubeRelease(podcast, episode) &&
            !IsYouTubeAuthorityAwaitingAudioRelease(podcast) &&
            !IsYouTubePodcastWithDelayedPosting(podcast))
        {
            return false;
        }

        return GetYouTubePublishingDelayExpiry(podcast, episode.Release, episode.Length) > DateTime.UtcNow;
    }

    public static bool IsAwaitingDelayedAudioRelease(this Podcast podcast, DateTime release, TimeSpan length)
    {
        if (!HasAudioPlatformConfigured(podcast) || !IsYouTubeAuthorityAwaitingAudioRelease(podcast))
        {
            return false;
        }

        return GetYouTubePublishingDelayExpiry(podcast, release, length) > DateTime.UtcNow;
    }

    private static bool HasAudioPlatformConfigured(Podcast podcast)
    {
        return !string.IsNullOrWhiteSpace(podcast.SpotifyId) || podcast.AppleId != null;
    }

    private static DateTime GetYouTubePublishingDelayExpiry(Podcast podcast, DateTime release, TimeSpan length)
    {
        var youTubePublishingDelay = podcast.YouTubePublishingDelay();
        if (youTubePublishingDelay > TimeSpan.Zero && length > TimeSpan.Zero)
        {
            youTubePublishingDelay = youTubePublishingDelay.Add(length);
        }

        return release.Add(youTubePublishingDelay);
    }

    private static bool IsAudioPodcastAwaitingYouTubeRelease(Podcast podcast, Episode episode)
    {
        return episode.Urls.YouTube == null && podcast.YouTubePublishingDelay() > TimeSpan.Zero;
    }

    private static bool IsYouTubeAuthorityAwaitingAudioRelease(Podcast podcast)
    {
        return podcast.ReleaseAuthority == Service.YouTube && podcast.YouTubePublishingDelay() > TimeSpan.Zero;
    }

    private static bool IsYouTubePodcastWithDelayedPosting(Podcast podcast)
    {
        return string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
               !podcast.AppleId.HasValue &&
               !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
               podcast.YouTubePublishingDelay() > TimeSpan.Zero;
    }
}