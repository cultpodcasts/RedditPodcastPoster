using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
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
            podcast.AppleId,
            podcast.YouTubePlaylistId);
    }

  public static bool IsScheduledYouTubeDiscoveryBypassed(this Podcast podcast, IndexingContext indexingContext)
    {
        return indexingContext.SkipYouTubeUrlResolving && podcast.DependsOnYouTubeForEpisodeDiscovery();
    }

    /// <param name="youTubePlaylistId">
    /// A playlist is a YouTube source on its own: a parent with a playlist and no channel id indexes from the playlist.
    /// </param>
    public static bool DependsOnYouTubeForEpisodeDiscovery(
        Service? releaseAuthority,
        string? youTubeChannelId,
        string? spotifyId,
        long? appleId,
        string? youTubePlaylistId = null)
    {
        if (string.IsNullOrWhiteSpace(youTubeChannelId) && string.IsNullOrWhiteSpace(youTubePlaylistId))
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

    /// <summary>
    /// True while an episode is waiting for its delayed YouTube upload, so social posting holds it back.
    /// <para>
    /// A positive <see cref="Podcast.YouTubePublicationOffset"/> means YouTube publishes that long
    /// <em>after</em> the audio. For a YouTube release-authority podcast the episode release is already
    /// the YouTube publish time, and the audio went out <c>release - offset</c>
    /// (see <c>EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup</c>), so there is nothing left to
    /// wait for once the YouTube episode exists (#1375).
    /// </para>
    /// </summary>
    public static bool IsDelayedYouTubePublishing(this Podcast podcast, Episode episode)
    {
        if (!HasAudioPlatformConfigured(podcast))
        {
            return false;
        }

        if (!IsAudioPodcastAwaitingYouTubeRelease(podcast, episode) &&
            !IsYouTubePodcastWithDelayedPosting(podcast))
        {
            return false;
        }

        return GetYouTubePublishingDelayExpiry(podcast, episode.ReleaseUtc, episode.Length) > DateTime.UtcNow;
    }

    private static bool HasAudioPlatformConfigured(Podcast podcast)
    {
        return !string.IsNullOrWhiteSpace(podcast.SpotifyId) || podcast.AppleId != null;
    }

    private static DateTime GetYouTubePublishingDelayExpiry(Podcast podcast, DateTime release, TimeSpan length)
    {
        var youTubePublishingDelay = podcast.YouTubePublishingDelay();
        // Episode length extends the delay only for YouTube-authority podcasts (an audio-discovered
        // episode still missing its YouTube upload). Audio-first podcasts publish to YouTube after
        // release + delay regardless of episode duration.
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
        return !EpisodeServicePresence.HasUrl(episode, ServiceKeys.YouTube) &&
               podcast.YouTubePublishingDelay() > TimeSpan.Zero;
    }


    private static bool IsYouTubePodcastWithDelayedPosting(Podcast podcast)
    {
        return string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
               !podcast.AppleId.HasValue &&
               podcast.HasYouTubeSource() &&
               podcast.YouTubePublishingDelay() > TimeSpan.Zero;
    }
}