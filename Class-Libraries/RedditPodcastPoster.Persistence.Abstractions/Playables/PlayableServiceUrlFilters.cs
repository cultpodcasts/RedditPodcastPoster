using System.Linq.Expressions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Persistence.Abstractions.Playables;

/// <summary>
/// Cosmos LINQ predicates that find the film, TV episode or news report already holding a service URL. Each takes the
/// URL as pasted and its stored form (for example <c>youtu.be/{id}</c> and <c>youtube.com/watch?v={id}</c>), so either
/// form matches with one query.
/// </summary>
public static class PlayableServiceUrlFilters
{
    /// <summary>Playables whose <paramref name="serviceKey"/> link equals <paramref name="url"/> or <paramref name="storedUrl"/>.</summary>
    public static Expression<Func<TPlayable, bool>> HoldsServiceUrl<TPlayable>(
        string serviceKey,
        Uri url,
        Uri storedUrl)
        where TPlayable : class, IPlayable =>
        item => item.Services != null &&
                (item.Services[serviceKey].Url == url || item.Services[serviceKey].Url == storedUrl);

    /// <summary>
    /// Films holding the URL as their <paramref name="serviceKey"/> link or as a trailer (<c>trailers[].url</c>). An
    /// episode attached as a trailer has its row deleted, so without the trailer match the same URL would be
    /// submitted or indexed again as a new episode.
    /// </summary>
    public static Expression<Func<Film, bool>> FilmHoldsServiceOrTrailerUrl(
        string serviceKey,
        Uri url,
        Uri storedUrl) =>
        film =>
            (film.Services != null &&
             (film.Services[serviceKey].Url == url || film.Services[serviceKey].Url == storedUrl)) ||
            (film.Trailers != null &&
             film.Trailers.Any(trailer => trailer.Url == url || trailer.Url == storedUrl));
}
