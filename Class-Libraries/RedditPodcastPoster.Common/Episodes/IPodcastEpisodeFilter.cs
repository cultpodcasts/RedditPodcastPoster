using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodeFilter
{
    IEnumerable<PodcastEpisode> GetNewEpisodesReleasedSince(IList<Podcast> podcasts, DateTime since);
    PodcastEpisode? GetMostRecentUntweetedEpisode(IList<Podcast> podcasts);
    bool IsRecentlyExpiredDelayedPublishing(Podcast podcast, Episode episode);
}