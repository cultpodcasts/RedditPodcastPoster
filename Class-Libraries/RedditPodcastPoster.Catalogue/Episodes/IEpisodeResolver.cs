using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Catalogue.Episodes;

public interface IEpisodeResolver
{
    Task<PodcastEpisode> ResolveServiceUrl(Uri url);
}
