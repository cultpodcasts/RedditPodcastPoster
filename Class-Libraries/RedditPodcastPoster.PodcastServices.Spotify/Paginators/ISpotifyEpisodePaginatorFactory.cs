using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Paginators;

/// <summary>
/// Creates the date-window paginators used by <see cref="SpotifyQueryPaginator"/>, so the walk
/// strategy (newest-first growth vs ascending end-jump) is chosen through one seam.
/// </summary>
public interface ISpotifyEpisodePaginatorFactory
{
    /// <summary>
    /// Newest-first catalogue walk that stops once releases fall before <paramref name="releasedSince"/>.
    /// </summary>
    IPaginator CreateReverseChronologicalPaginator(DateTime? releasedSince);

    /// <summary>
    /// Oldest-first (expensive) catalogue walk that jumps to the final Spotify page and pages
    /// backwards through the <paramref name="releasedSince"/> window.
    /// </summary>
    IPaginator CreateAscendingEndJumpPaginator(DateTime releasedSince);
}
