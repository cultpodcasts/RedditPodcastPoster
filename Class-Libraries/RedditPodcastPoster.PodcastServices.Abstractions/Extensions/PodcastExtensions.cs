using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

public static class PodcastExtensions
{
    public static bool DependsOnYouTubeForEpisodeDiscovery(this Podcast podcast)
    {
        return DependsOnYouTubeForEpisodeDiscovery(
            podcast.ReleaseAuthority,
            podcast.YouTubeChannelId,
            podcast.SpotifyId,
            podcast.AppleId);
    }

  public static bool IsScheduledYouTubeDiscoveryBypassed(this Podcast podcast, IndexingContext indexingContext)
    {
        return indexingContext.SkipYouTubeUrlResolving && podcast.DependsOnYouTubeForEpisodeDiscovery();
    }

    public static bool DependsOnYouTubeForEpisodeDiscovery(
        Service? releaseAuthority,
        string? youTubeChannelId,
        string? spotifyId,
        long? appleId)
    {
        if (string.IsNullOrWhiteSpace(youTubeChannelId))
        {
            return false;
        }

        if (releaseAuthority == Service.YouTube)
        {
            return true;
        }

        var hasSpotifyDiscovery = releaseAuthority is null or Service.Spotify &&
                                  !string.IsNullOrWhiteSpace(spotifyId);
        var hasAppleDiscovery = releaseAuthority != Service.YouTube && appleId != null;

        return !hasSpotifyDiscovery && !hasAppleDiscovery;
    }

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
        // Episode length extends the delay only for YouTube-authority podcasts awaiting delayed
        // audio release. Audio-first podcasts publish to YouTube after release + delay regardless
        // of episode duration.
        if (podcast.ReleaseAuthority == Service.YouTube &&
            youTubePublishingDelay > TimeSpan.Zero &&
            length > TimeSpan.Zero)
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