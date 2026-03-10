using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// Filters detached episodes using `IEpisodeRepository` and returns `PodcastEpisode` pairs.
/// </summary>
public interface IPodcastEpisodeFilterV2
{
    Task<IEnumerable<PodcastEpisode>> GetNewEpisodesReleasedSince(
        Guid podcastId,
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    Task<IEnumerable<PodcastEpisode>> GetMostRecentUntweetedEpisodes(
        Guid podcastId,
        int numberOfDays);

    Task<IEnumerable<PodcastEpisode>> GetMostRecentBlueskyReadyEpisodes(
        Guid podcastId,
        int numberOfDays);

    bool IsRecentlyExpiredDelayedPublishing(Podcast podcast, Episode episode);
}

