using System.Linq.Expressions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;

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
    /// Films holding the URL as their <paramref name="serviceKey"/> link or as a trailer. An episode attached as a
    /// trailer has its row deleted, so without the trailer match the same URL would be submitted or indexed again as a
    /// new episode. Trailer match covers <c>trailers[].url</c> (playback), <c>trailers[].services[{key}].url</c>
    /// (source-episode platform URLs), and <c>trailers[].ids</c> for the platform of <paramref name="serviceKey"/>
    /// when the matching id is supplied.
    /// </summary>
    /// <remarks>
    /// The four platform bodies must stay in lockstep: trailer.Url forms, Ids-before-Services[key] (so Compile() can
    /// match on ids when that key is absent from a partial services map), and the film.Services arm. Drift detectors:
    /// <c>PlayableServiceUrlFilterTranslationRules</c>, <c>UrlMembershipLookupRules</c>, and
    /// <c>IndexerSkipsCatalogueOwnedUrlsRules</c> Compile() paths.
    /// </remarks>
    public static Expression<Func<Film, bool>> FilmHoldsServiceOrTrailerUrl(
        string serviceKey,
        Uri url,
        Uri storedUrl,
        string? spotifyId = null,
        long? appleId = null,
        string? youTubeId = null)
    {
        if (serviceKey == ServiceKeys.Spotify && !string.IsNullOrWhiteSpace(spotifyId))
        {
            var id = spotifyId;
            return film =>
                (film.Services != null &&
                 (film.Services[serviceKey].Url == url || film.Services[serviceKey].Url == storedUrl)) ||
                (film.Trailers != null &&
                 film.Trailers.Any(trailer =>
                     trailer.Url == url ||
                     trailer.Url == storedUrl ||
                     (trailer.Ids != null && trailer.Ids.Spotify == id) ||
                     (trailer.Services != null &&
                      (trailer.Services[serviceKey].Url == url || trailer.Services[serviceKey].Url == storedUrl))));
        }

        if (serviceKey == ServiceKeys.Apple && appleId is > 0)
        {
            var id = appleId.Value;
            return film =>
                (film.Services != null &&
                 (film.Services[serviceKey].Url == url || film.Services[serviceKey].Url == storedUrl)) ||
                (film.Trailers != null &&
                 film.Trailers.Any(trailer =>
                     trailer.Url == url ||
                     trailer.Url == storedUrl ||
                     (trailer.Ids != null && trailer.Ids.Apple == id) ||
                     (trailer.Services != null &&
                      (trailer.Services[serviceKey].Url == url || trailer.Services[serviceKey].Url == storedUrl))));
        }

        if (serviceKey == ServiceKeys.YouTube && !string.IsNullOrWhiteSpace(youTubeId))
        {
            var id = youTubeId;
            return film =>
                (film.Services != null &&
                 (film.Services[serviceKey].Url == url || film.Services[serviceKey].Url == storedUrl)) ||
                (film.Trailers != null &&
                 film.Trailers.Any(trailer =>
                     trailer.Url == url ||
                     trailer.Url == storedUrl ||
                     (trailer.Ids != null && trailer.Ids.YouTube == id) ||
                     (trailer.Services != null &&
                      (trailer.Services[serviceKey].Url == url || trailer.Services[serviceKey].Url == storedUrl))));
        }

        return film =>
            (film.Services != null &&
             (film.Services[serviceKey].Url == url || film.Services[serviceKey].Url == storedUrl)) ||
            (film.Trailers != null &&
             film.Trailers.Any(trailer =>
                 trailer.Url == url ||
                 trailer.Url == storedUrl ||
                 (trailer.Services != null &&
                  (trailer.Services[serviceKey].Url == url || trailer.Services[serviceKey].Url == storedUrl))));
    }
}
