using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeResolver
{
    Task<ResolvedPodcastEpisode> ResolveServiceUrl(Uri url);
    Task<IEnumerable<ResolvedPodcastEpisode>> ResolveSinceReleaseDate(DateTime since);
}