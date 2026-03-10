using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeResolver
{
    Task<PodcastEpisodeV2> ResolveServiceUrl(Uri url);
}