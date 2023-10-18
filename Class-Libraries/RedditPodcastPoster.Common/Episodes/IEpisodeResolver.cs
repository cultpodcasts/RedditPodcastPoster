using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeResolver
{
    Task<PodcastEpisode> ResolveServiceUrl(Uri url);
    Task<IEnumerable<PodcastEpisode>> ResolveSinceReleaseDate(DateTime since);
}