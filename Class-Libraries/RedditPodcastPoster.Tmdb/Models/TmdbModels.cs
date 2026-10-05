using System.Globalization;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Tmdb.Models;

public enum TmdbTitleKind
{
    Movie,
    TvSeries,
    TvEpisode
}

public enum CatalogueCanonicalSource
{
    Imdb,
    Tvdb
}

/// <summary>
/// The identity link for a film, series, or episode. An IMDb title id wins.
/// A TheTVDB id is used only when TMDB has no IMDb title id.
/// </summary>
public sealed partial record CatalogueCanonicalId(string Id, CatalogueCanonicalSource Source, Uri Url)
{
    public static CatalogueCanonicalId? Resolve(string? imdbId, long? tvdbId, TmdbTitleKind kind)
    {
        if (imdbId is not null && ImdbTitleId().IsMatch(imdbId))
        {
            return new CatalogueCanonicalId(imdbId, CatalogueCanonicalSource.Imdb, ImdbPage(imdbId));
        }

        if (tvdbId is > 0)
        {
            var id = tvdbId.Value.ToString(CultureInfo.InvariantCulture);
            return new CatalogueCanonicalId(id, CatalogueCanonicalSource.Tvdb, TvdbPage(tvdbId.Value, kind));
        }

        return null;
    }

    public static Uri ImdbPage(string imdbId) => new($"https://www.imdb.com/title/{imdbId}/");

    /// <summary>
    /// TheTVDB numeric redirect. TMDB stores the id, not the slug.
    /// </summary>
    public static Uri TvdbPage(long tvdbId, TmdbTitleKind kind)
    {
        var page = kind switch
        {
            TmdbTitleKind.Movie => "movie",
            TmdbTitleKind.TvSeries => "series",
            TmdbTitleKind.TvEpisode => "episode",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return new Uri($"https://www.thetvdb.com/dereferrer/{page}/{tvdbId.ToString(CultureInfo.InvariantCulture)}");
    }

    [GeneratedRegex("^tt[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ImdbTitleId();
}

/// <summary>A TMDB search hit. Canonical ids come from the detail calls, which load external ids.</summary>
public sealed record TmdbSearchHit(int Id, string Name, TmdbTitleKind Kind, int? Year);

/// <summary>
/// A TMDB movie, series, or episode with both external ids when TMDB has them.
/// <see cref="Canonical"/> is the IMDb title when present, otherwise the TheTVDB id.
/// </summary>
public sealed record TmdbTitle(
    int Id,
    string Name,
    TmdbTitleKind Kind,
    int? Year,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? ImdbId,
    long? TvdbId,
    Uri? ImdbUrl,
    Uri? TvdbUrl,
    CatalogueCanonicalId? Canonical);
