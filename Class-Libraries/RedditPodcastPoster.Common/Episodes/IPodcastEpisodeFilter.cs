using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodeFilter
{
    IEnumerable<PodcastEpisode> GetNewEpisodesReleasedSince(
        IList<Podcast> podcasts, 
        DateTime since,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true);

    PodcastEpisode? GetMostRecentUntweetedEpisode(
        IList<Podcast> podcasts,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true,
        int? numberOfDays = null);

    bool IsRecentlyExpiredDelayedPublishing(
        Podcast podcast,
        Episode episode);
}