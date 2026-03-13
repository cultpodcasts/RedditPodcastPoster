using RedditPodcastPoster.Models;
using Episode = RedditPodcastPoster.Models.V2.Episode;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodeFilter
{
    Task<IEnumerable<PodcastEpisode>> GetNewEpisodesReleasedSince(
        IEnumerable<PodcastEpisode> podcastEpisodes,
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    Task<IEnumerable<PodcastEpisode>> GetMostRecentUntweetedEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        int numberOfDays);

    Task<IEnumerable<PodcastEpisode>> GetMostRecentUntweetedEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        int numberOfDays);

    bool IsRecentlyExpiredDelayedPublishing(
        Podcast podcast,
        Episode episode);

    Task<IEnumerable<PodcastEpisode>> GetMostRecentBlueskyReadyEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        int numberOfDays);

    Task<IEnumerable<PodcastEpisode>> GetMostRecentBlueskyReadyEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        int numberOfDays);
}
