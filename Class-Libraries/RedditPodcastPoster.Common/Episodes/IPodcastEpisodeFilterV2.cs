using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// V2 version of IPodcastEpisodeFilter that works with detached episodes via IEpisodeRepository.
/// Episodes are loaded from the detached repository instead of podcast.Episodes collection.
/// </summary>
public interface IPodcastEpisodeFilterV2
{
    /// <summary>
    /// Gets episodes that are ready to post based on release date and other criteria.
    /// Returns V2 models.
    /// </summary>
    Task<IEnumerable<PodcastEpisodeV2>> GetNewEpisodesReleasedSinceV2(
        Guid podcastId,
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    /// <summary>
    /// Gets the most recent episodes that haven't been tweeted yet.
    /// Returns V2 models.
    /// </summary>
    Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentUntweetedEpisodesV2(
        Guid podcastId,
        int numberOfDays);

    /// <summary>
    /// Gets the most recent episodes that are ready for Bluesky posting.
    /// Returns V2 models.
    /// </summary>
    Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentBlueskyReadyEpisodesV2(
        Guid podcastId,
        int numberOfDays);

    /// <summary>
    /// Gets episodes that are ready to post based on release date and other criteria.
    /// Returns legacy models for backward compatibility.
    /// </summary>
    Task<IEnumerable<PodcastEpisode>> GetNewEpisodesReleasedSince(
        Guid podcastId,
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    /// <summary>
    /// Gets the most recent episodes that haven't been tweeted yet.
    /// Returns legacy models for backward compatibility.
    /// </summary>
    Task<IEnumerable<PodcastEpisode>> GetMostRecentUntweetedEpisodes(
        Guid podcastId,
        int numberOfDays);

    /// <summary>
    /// Gets the most recent episodes that are ready for Bluesky posting.
    /// Returns legacy models for backward compatibility.
    /// </summary>
    Task<IEnumerable<PodcastEpisode>> GetMostRecentBlueskyReadyEpisodes(
        Guid podcastId,
        int numberOfDays);

    /// <summary>
    /// Checks if an episode's delayed publishing window recently expired.
    /// </summary>
    bool IsRecentlyExpiredDelayedPublishing(Podcast podcast, Episode episode);
}
