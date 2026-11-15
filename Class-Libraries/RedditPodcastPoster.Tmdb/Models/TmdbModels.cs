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
/// A TheTVDB id is used only for a series or episode when TMDB has no IMDb title id.
/// A film never uses a TheTVDB page.
/// </summary>
public sealed partial record CatalogueCanonicalId(string Id, CatalogueCanonicalSource Source, Uri Url)
{
    public static CatalogueCanonicalId? Resolve(string? imdbId, long? tvdbId, TmdbTitleKind kind)
    {
        if (imdbId is not null && ImdbTitleId().IsMatch(imdbId))
        {
            return new CatalogueCanonicalId(imdbId, CatalogueCanonicalSource.Imdb, ImdbPage(imdbId));
        }

        if (kind == TmdbTitleKind.Movie || tvdbId is not > 0)
        {
            return null;
        }

        var id = tvdbId.Value.ToString(CultureInfo.InvariantCulture);
        return new CatalogueCanonicalId(id, CatalogueCanonicalSource.Tvdb, TvdbPage(tvdbId.Value, kind));
    }

    public static Uri ImdbPage(string imdbId) => new($"https://www.imdb.com/title/{imdbId}/");

    /// <summary>
    /// TheTVDB numeric redirect for a series or episode. TMDB stores the id, not the slug.
    /// Films do not use this page.
    /// </summary>
    public static Uri TvdbPage(long tvdbId, TmdbTitleKind kind)
    {
        var page = kind switch
        {
            TmdbTitleKind.TvSeries => "series",
            TmdbTitleKind.TvEpisode => "episode",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "TheTVDB pages are for series and episodes.")
        };
        return new Uri($"https://www.thetvdb.com/dereferrer/{page}/{tvdbId.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>TMDB movie page for curator inspection.</summary>
    public static Uri TmdbMoviePage(int tmdbId) =>
        new($"https://www.themoviedb.org/movie/{tmdbId.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>TMDB TV series page for curator inspection.</summary>
    public static Uri TmdbTvPage(int tmdbId) =>
        new($"https://www.themoviedb.org/tv/{tmdbId.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>w185 poster URL when <paramref name="posterPath"/> is a TMDB path (leading slash).</summary>
    public static Uri? TmdbPosterThumbnail(string? posterPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath))
        {
            return null;
        }

        var path = posterPath.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return new Uri($"https://image.tmdb.org/t/p/w185{path}");
    }

    [GeneratedRegex("^tt[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ImdbTitleId();
}

/// <summary>A TMDB search hit. Canonical ids come from the detail calls, which load external ids.</summary>
/// <param name="OriginalName">
/// TMDB <c>original_title</c> (films) or <c>original_name</c> (TV) when it differs from <see cref="Name"/>.
/// A Danish film listed as "Father's House" keeps "Faderhuset" here.
/// </param>
public sealed record TmdbSearchHit(
    int Id,
    string Name,
    TmdbTitleKind Kind,
    int? Year,
    string? PosterPath = null,
    string? Overview = null,
    string? OriginalName = null);

/// <summary>
/// A TMDB movie, series, or episode with both external ids when TMDB has them.
/// <see cref="Canonical"/> is the IMDb title when present. For a series or episode
/// with no IMDb title id, it is the TheTVDB id. A film has no TheTVDB canonical.
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

/// <summary>
/// A TMDB movie's own metadata (RPP #1430 / #1431). <see cref="Image"/> is the landscape backdrop when TMDB has one,
/// else the poster. <see cref="OtherTitles"/> is every other title the film is known by, distinct and trimmed:
/// the original title, TMDB's alternative titles and its translated titles, never <see cref="Title"/> itself.
/// </summary>
public sealed record TmdbMovieMetadata(
    int Id,
    string Title,
    string? OriginalTitle,
    string? Overview,
    int? RuntimeMinutes,
    DateOnly? ReleaseDate,
    Uri? Image,
    IReadOnlyList<string> OtherTitles);

/// <summary>A TMDB TV episode's own metadata (RPP #1430). <see cref="Still"/> is the episode still.</summary>
public sealed record TmdbEpisodeMetadata(
    int Id,
    string? Name,
    string? Overview,
    int? RuntimeMinutes,
    DateOnly? AirDate,
    Uri? Still);

/// <summary>Where a TV episode sits on TMDB: its series id, season and episode number.</summary>
public sealed record TmdbEpisodeReference(int SeriesId, int SeasonNumber, int EpisodeNumber);

/// <summary>TMDB image URLs for the paths in a details payload.</summary>
public static class TmdbImage
{
    private const string Base = "https://image.tmdb.org/t/p/";

    public static Uri? Backdrop(string? path) => Build("w1280", path);

    public static Uri? Poster(string? path) => Build("w780", path);

    public static Uri? Still(string? path) => Build("w780", path);

    private static Uri? Build(string size, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim();
        return new Uri(Base + size + (trimmed.StartsWith('/') ? trimmed : "/" + trimmed));
    }
}
