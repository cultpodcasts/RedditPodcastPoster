using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

/// <summary>
/// One page of a show's newest episodes, fetched at most once per show per podcast pass.
/// </summary>
public interface ISpotifyRecentShowEpisodesCache
{
    Task<IReadOnlyList<SimpleEpisode>> GetRecentShowEpisodes(
        string showId,
        string market,
        int pageSize,
        IndexingContext indexingContext,
        CancellationToken cancellationToken = default);
}
