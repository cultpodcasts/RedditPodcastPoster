using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeResolver
{
    Task<ResolvedPodcastEpisode> ResolveSpotifyUrl(Uri spotifyUrl);
    Task<IEnumerable<ResolvedPodcastEpisode>> ResolveSinceReleaseDate(DateTime since);
}