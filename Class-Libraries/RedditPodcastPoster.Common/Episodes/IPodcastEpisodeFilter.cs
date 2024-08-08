using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodeFilter
{
    IEnumerable<PodcastEpisode> GetNewEpisodesReleasedSince(
        IEnumerable<Podcast> podcasts,
        DateTime since,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true);

    IEnumerable<PodcastEpisode> GetMostRecentUntweetedEpisodes(
        Podcast podcast,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true,
        int numberOfDays = 1);

    bool IsRecentlyExpiredDelayedPublishing(
        Podcast podcast,
        Episode episode);
}