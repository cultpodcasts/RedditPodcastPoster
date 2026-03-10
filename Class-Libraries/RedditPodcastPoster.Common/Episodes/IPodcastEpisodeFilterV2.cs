using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// V2 version of IPodcastEpisodeFilter that works with detached episodes via IEpisodeRepository.
/// Returns PodcastEpisode with V2 models. For legacy PodcastEpisode, use IPodcastEpisodeFilter.
/// </summary>
public interface IPodcastEpisodeFilterV2
{
    /// <summary>
    /// Gets episodes that are ready to post based on release date and other criteria.
    /// </summary>
    Task<IEnumerable<PodcastEpisode>> GetNewEpisodesReleasedSince(
        Guid podcastId,
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    /// <summary>
    /// Gets the most recent episodes that haven't been tweeted yet.
    /// </summary>
    Task<IEnumerable<PodcastEpisode>> GetMostRecentUntweetedEpisodes(
        Guid podcastId,
        int numberOfDays);

    /// <summary>
    /// Gets the most recent episodes that are ready for Bluesky posting.
    /// </summary>
    Task<IEnumerable<PodcastEpisode>> GetMostRecentBlueskyReadyEpisodes(
        Guid podcastId,
        int numberOfDays);

    /// <summary>
    /// Checks if an episode's delayed publishing window recently expired.
    /// </summary>
    bool IsRecentlyExpiredDelayedPublishing(Podcast podcast, Episode episode);
}

