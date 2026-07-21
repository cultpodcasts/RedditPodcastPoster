using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeResolver
{
    Task<PodcastEpisode> ResolveServiceUrl(Uri url);
}
