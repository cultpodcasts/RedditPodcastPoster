using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodeFilter
{
    IEnumerable<PodcastEpisode> GetNewEpisodesReleasedSince(
        IEnumerable<Podcast> podcasts,
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    IEnumerable<PodcastEpisode> GetMostRecentUntweetedEpisodes(
        Podcast podcast,
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        int numberOfDays);

    bool IsRecentlyExpiredDelayedPublishing(
        Podcast podcast,
        Episode episode);

    IEnumerable<PodcastEpisode> GetMostRecentBlueskyReadyEpisodes(
        Podcast podcast,
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        int numberOfDays);
}