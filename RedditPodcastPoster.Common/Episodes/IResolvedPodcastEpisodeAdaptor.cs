using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IResolvedPodcastEpisodeAdaptor
{
    PostModel ToPostModel(Podcast podcast, IEnumerable<Episode> episodes);
}