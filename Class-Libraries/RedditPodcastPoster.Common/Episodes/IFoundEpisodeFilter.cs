using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.Common.Episodes;

public interface IFoundEpisodeFilter
{
    IList<Episode> ReduceEpisodes(Podcast podcast, IList<Episode> episodes);
}