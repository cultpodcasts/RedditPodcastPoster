using RedditPodcastPoster.Models;
using Episode = RedditPodcastPoster.Models.V2.Episode;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodeFilter
{
    Task<IEnumerable<PodcastEpisodeV2>> GetNewEpisodesReleasedSince(
        IEnumerable<Podcast> podcasts,
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentUntweetedEpisodes(
        Podcast podcast,
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        int numberOfDays);

    Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentUntweetedEpisodes(
        Podcast podcast,
        int numberOfDays);

    bool IsRecentlyExpiredDelayedPublishing(
        Podcast podcast,
        Episode episode);

    Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentBlueskyReadyEpisodes(
        Podcast podcast,
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        int numberOfDays);

    Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentBlueskyReadyEpisodes(
        Podcast podcast,
        int numberOfDays);
}