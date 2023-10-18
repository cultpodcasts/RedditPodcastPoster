using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IFoundEpisodeFilter
{
    IList<Episode> ReduceEpisodes(Podcast podcast, IList<Episode> episodes);
}