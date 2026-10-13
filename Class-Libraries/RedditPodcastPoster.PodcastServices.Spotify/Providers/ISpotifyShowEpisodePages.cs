using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

/// <summary>
/// Newest-first pages of a Spotify show (50 episodes, Spotify's maximum), each fetched at most once per podcast pass.
/// Oldest-first ("expensive") catalogues are read from the end using <c>Paging.Total</c>, so page 0 is always the newest.
/// </summary>
public interface ISpotifyShowEpisodePages
{
    Task<CatalogueShowPage<SimpleEpisode>?> GetNewestPage(
        string showId,
        string market,
        int pageIndex,
        IndexingContext indexingContext,
        CancellationToken cancellationToken = default);
}
