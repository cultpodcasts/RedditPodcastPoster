using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Common.Episodes;

public interface IFoundEpisodeFilter
{
    IList<Episode> ReduceEpisodes(Podcast podcast, IList<Episode> episodes);
}
