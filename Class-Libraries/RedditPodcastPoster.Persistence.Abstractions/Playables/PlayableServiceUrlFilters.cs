using System.Linq.Expressions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;

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
    /// Like <see cref="HoldsServiceUrl{TPlayable}"/>, and when <paramref name="serviceKey"/> is YouTube also
    /// matches an <c>additionalYouTube</c> entry by video id or url (RPP #1411).
    /// </summary>
    public static Expression<Func<TPlayable, bool>> HoldsServiceUrlOrYouTubeAlternate<TPlayable>(
        string serviceKey,
        Uri url,
        Uri storedUrl,
        string? youTubeId = null)
        where TPlayable : class, IPlayable
    {
        if (serviceKey == ServiceKeys.YouTube && !string.IsNullOrWhiteSpace(youTubeId))
        {
            var id = youTubeId;
            return item =>
                (item.Services != null &&
                 (item.Services[serviceKey].Url == url || item.Services[serviceKey].Url == storedUrl)) ||
                (item.AdditionalYouTube != null &&
                 item.AdditionalYouTube.Any(alternate =>
                     alternate.YouTubeId == id || alternate.Url == url || alternate.Url == storedUrl));
        }

        return HoldsServiceUrl<TPlayable>(serviceKey, url, storedUrl);
    }

    /// <summary>
    /// Playables that have a <paramref name="serviceKey"/> link at all. In Cosmos a missing key is undefined, so
    /// <c>!= null</c> is false for it (in-memory callers must only pass items that have the key or guard it).
    /// </summary>
    public static Expression<Func<TPlayable, bool>> HasServiceLink<TPlayable>(string serviceKey)
        where TPlayable : class, IPlayable =>
        item => item.Services != null && item.Services[serviceKey] != null;

    /// <summary>
    /// Projects only the stored <paramref name="serviceKey"/> URL (<c>SELECT VALUE c.services[key].url</c>), so a
    /// caller can compare stored URLs in memory without reading whole documents (#1517 legacy Netflix title URLs).
    /// </summary>
    public static Expression<Func<TPlayable, Uri?>> ServiceUrl<TPlayable>(string serviceKey)
        where TPlayable : class, IPlayable =>
        item => item.Services![serviceKey].Url;

    /// <summary>
    /// Films holding the URL as their <paramref name="serviceKey"/> link or as a trailer. An episode attached as a
    /// trailer or as an additional YouTube upload (RPP #1411) has its row deleted, so without those matches the same URL would be submitted or indexed again as a
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
                (film.AdditionalYouTube != null &&
                 film.AdditionalYouTube.Any(alternate =>
                     alternate.YouTubeId == id || alternate.Url == url || alternate.Url == storedUrl)) ||
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

    /// <summary>
    /// TV shows holding the URL as a trailer (RPP #1412). The same trailer match as
    /// <see cref="FilmHoldsServiceOrTrailerUrl"/>: <c>trailers[].url</c> (playback),
    /// <c>trailers[].services[{key}].url</c> (source-episode platform URLs), and <c>trailers[].ids</c> for the platform
    /// of <paramref name="serviceKey"/> when the matching id is supplied. A TV show has no <c>services</c> or
    /// <c>additionalYouTube</c> of its own, so those arms are absent. A trailer's source episode row is deleted on
    /// attach, so without this match the same URL would be submitted or indexed again as a new episode.
    /// </summary>
    /// <remarks>
    /// Keep the platform bodies in lockstep with <see cref="FilmHoldsServiceOrTrailerUrl"/>. Drift detectors:
    /// <c>PlayableServiceUrlFilterTranslationRules</c> and <c>UrlMembershipLookupRules</c> Compile() paths.
    /// </remarks>
    public static Expression<Func<TvShow, bool>> TvShowHoldsTrailerUrl(
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
            return show =>
                show.Trailers != null &&
                show.Trailers.Any(trailer =>
                    trailer.Url == url ||
                    trailer.Url == storedUrl ||
                    (trailer.Ids != null && trailer.Ids.Spotify == id) ||
                    (trailer.Services != null &&
                     (trailer.Services[serviceKey].Url == url || trailer.Services[serviceKey].Url == storedUrl)));
        }

        if (serviceKey == ServiceKeys.Apple && appleId is > 0)
        {
            var id = appleId.Value;
            return show =>
                show.Trailers != null &&
                show.Trailers.Any(trailer =>
                    trailer.Url == url ||
                    trailer.Url == storedUrl ||
                    (trailer.Ids != null && trailer.Ids.Apple == id) ||
                    (trailer.Services != null &&
                     (trailer.Services[serviceKey].Url == url || trailer.Services[serviceKey].Url == storedUrl)));
        }

        if (serviceKey == ServiceKeys.YouTube && !string.IsNullOrWhiteSpace(youTubeId))
        {
            var id = youTubeId;
            return show =>
                show.Trailers != null &&
                show.Trailers.Any(trailer =>
                    trailer.Url == url ||
                    trailer.Url == storedUrl ||
                    (trailer.Ids != null && trailer.Ids.YouTube == id) ||
                    (trailer.Services != null &&
                     (trailer.Services[serviceKey].Url == url || trailer.Services[serviceKey].Url == storedUrl)));
        }

        return show =>
            show.Trailers != null &&
            show.Trailers.Any(trailer =>
                trailer.Url == url ||
                trailer.Url == storedUrl ||
                (trailer.Services != null &&
                 (trailer.Services[serviceKey].Url == url || trailer.Services[serviceKey].Url == storedUrl)));
    }}
