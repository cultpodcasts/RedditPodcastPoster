using System.Linq.Expressions;
using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Persistence.Abstractions.Episodes;

/// <summary>Cosmos LINQ predicates over the films container.</summary>
public static class FilmCosmosFilters
{
    /// <summary>
    /// Films that store <paramref name="url"/> as their <paramref name="serviceKey"/> link, or hold a trailer whose
    /// URL equals <paramref name="url"/> or <paramref name="trailerUrl"/>. An episode attached as a trailer has its
    /// row deleted, so without the trailer match the same URL would be submitted or indexed again as a new episode.
    /// </summary>
    public static Expression<Func<Film, bool>> HoldsServiceOrTrailerUrl(
        string serviceKey,
        Uri url,
        Uri trailerUrl) =>
        film =>
            (film.Services != null && film.Services[serviceKey].Url == url) ||
            (film.Trailers != null &&
             film.Trailers.Any(trailer => trailer.Url == url || trailer.Url == trailerUrl));
}
