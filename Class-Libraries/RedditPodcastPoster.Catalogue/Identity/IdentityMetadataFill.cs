using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Catalogue.Identity;

/// <summary>
/// Fields a curator set in the same PATCH, including clearing them. Identity fill must not write these: only fields
/// that were already empty and were not touched in this request are filled (RPP #1430).
/// </summary>
public readonly record struct IdentityFieldSkip(
    bool Title = false,
    bool Description = false,
    bool Release = false,
    bool Length = false,
    bool Image = false,
    bool AlternativeTitles = false,
    bool Publisher = false,
    bool TmdbId = false);

/// <summary>
/// Fills a film or TV episode from its identity (TMDB) metadata, only where the field is empty (RPP #1430, #1431).
/// A value that is already set, by a streaming service or a curator, is never overwritten. A field the curator
/// cleared or set in the same PATCH is also left alone.
/// </summary>
public static class IdentityMetadataFill
{
    /// <summary>
    /// True when the film has an empty field that identity metadata can fill: description, release, runtime,
    /// image (when no streaming service has one either), publisher, or alternative titles (never set; a cleared or
    /// "checked, none found" list is kept, unless <paramref name="refillEmptyAlternativeTitles"/> is set because this
    /// PATCH changes the IMDb or TMDB id and does not send alternativeTitles). Fields in <paramref name="skip"/> are
    /// not treated as gaps.
    /// </summary>
    public static bool HasGaps(Film film, IdentityFieldSkip skip = default, bool refillEmptyAlternativeTitles = false)
    {
        ArgumentNullException.ThrowIfNull(film);
        return (!skip.Description && string.IsNullOrWhiteSpace(film.Description)) ||
               (!skip.Release && film.Release is null) ||
               (!skip.Length && film.Length <= TimeSpan.Zero) ||
               (!skip.Image && film.Image is null && !HasServiceImage(film.Services)) ||
               (!skip.Publisher && string.IsNullOrWhiteSpace(film.PublisherName)) ||
               (!skip.AlternativeTitles && AlternativeTitlesNeedFill(film, refillEmptyAlternativeTitles));
    }

    /// <summary>
    /// True when the TV show's publisher is empty and can be filled from identity metadata (RPP #1436). Fields in
    /// <paramref name="skip"/> are not treated as gaps.
    /// </summary>
    public static bool HasGaps(TvShow show, IdentityFieldSkip skip = default)
    {
        ArgumentNullException.ThrowIfNull(show);
        return !skip.Publisher && string.IsNullOrWhiteSpace(show.PublisherName);
    }

    /// <summary>
    /// True when the episode has an empty field that identity metadata can fill: name, overview, air date, runtime or
    /// image (when no streaming service has one either). Fields in <paramref name="skip"/> are not treated as gaps.
    /// </summary>
    public static bool HasGaps(TvShowEpisode episode, IdentityFieldSkip skip = default)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return (!skip.Title && string.IsNullOrWhiteSpace(episode.Title)) ||
               (!skip.Description && string.IsNullOrWhiteSpace(episode.Description)) ||
               (!skip.Release && episode.Release is null) ||
               (!skip.Length && episode.Length <= TimeSpan.Zero) ||
               (!skip.Image && episode.Image is null && !HasServiceImage(episode.Services));
    }

    /// <summary>Fills the film's empty, unskipped fields from <paramref name="metadata"/>. True when anything changed.</summary>
    public static bool Fill(Film film, TmdbMovieMetadata metadata, IdentityFieldSkip skip = default, bool refillEmptyAlternativeTitles = false)
    {
        ArgumentNullException.ThrowIfNull(film);
        ArgumentNullException.ThrowIfNull(metadata);
        var changed = false;
        if (!skip.TmdbId && film.TmdbId is null && metadata.Id > 0)
        {
            film.TmdbId = metadata.Id;
            changed = true;
        }

        if (!skip.Description && string.IsNullOrWhiteSpace(film.Description) && metadata.Overview is { } overview)
        {
            film.Description = overview;
            changed = true;
        }

        if (!skip.Release && film.Release is null && metadata.ReleaseDate is { } released)
        {
            film.SetRelease(CatalogueRelease.FromDate(released));
            changed = true;
        }

        if (!skip.Length && film.Length <= TimeSpan.Zero && metadata.RuntimeMinutes is int minutes and > 0)
        {
            film.Length = TimeSpan.FromMinutes(minutes);
            changed = true;
        }

        if (!skip.Image && film.Image is null && metadata.Image is { } image)
        {
            film.Image = image;
            changed = true;
        }

        if (!skip.Publisher && string.IsNullOrWhiteSpace(film.PublisherName) && metadata.Publisher is { } publisher)
        {
            film.PublisherName = publisher;
            changed = true;
        }

        // Null means never set: fill, or store [] when a successful lookup found no other titles, so the next
        // edit does not call TMDB again. [] means a curator cleared them, or TMDB had none; it is not filled again,
        // unless this PATCH changes the IMDb or TMDB id and does not send alternativeTitles.
        if (!skip.AlternativeTitles && AlternativeTitlesNeedFill(film, refillEmptyAlternativeTitles))
        {
            film.AlternativeTitles = metadata.OtherTitles
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title.Trim())
                .Where(title => !string.Equals(title, film.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            changed = true;
        }

        return changed;
    }

    /// <summary>Fills the episode's empty, unskipped fields from <paramref name="metadata"/>. True when anything changed.</summary>
    public static bool Fill(TvShowEpisode episode, TmdbEpisodeMetadata metadata, IdentityFieldSkip skip = default)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(metadata);
        var changed = false;
        if (!skip.TmdbId && episode.TmdbId is null && metadata.Id > 0)
        {
            episode.TmdbId = metadata.Id;
            changed = true;
        }

        if (!skip.Title && string.IsNullOrWhiteSpace(episode.Title) && metadata.Name is { } name)
        {
            episode.Title = name;
            changed = true;
        }

        if (!skip.Description && string.IsNullOrWhiteSpace(episode.Description) && metadata.Overview is { } overview)
        {
            episode.Description = overview;
            changed = true;
        }

        if (!skip.Release && episode.Release is null && metadata.AirDate is { } aired)
        {
            episode.SetRelease(CatalogueRelease.FromDate(aired));
            changed = true;
        }

        if (!skip.Length && episode.Length <= TimeSpan.Zero && metadata.RuntimeMinutes is int minutes and > 0)
        {
            episode.Length = TimeSpan.FromMinutes(minutes);
            changed = true;
        }

        if (!skip.Image && episode.Image is null && metadata.Still is { } still)
        {
            episode.Image = still;
            changed = true;
        }

        return changed;
    }

    /// <summary>Fills the show's empty, unskipped publisher from <paramref name="metadata"/>. True when it changed.</summary>
    public static bool Fill(TvShow show, TmdbShowMetadata metadata, IdentityFieldSkip skip = default)
    {
        ArgumentNullException.ThrowIfNull(show);
        ArgumentNullException.ThrowIfNull(metadata);
        if (!skip.TmdbId && show.TmdbId is null && metadata.Id > 0)
        {
            show.TmdbId = metadata.Id;
        }

        if (!skip.Publisher && string.IsNullOrWhiteSpace(show.PublisherName) && metadata.Publisher is { } publisher)
        {
            show.PublisherName = publisher;
            return true;
        }

        return false;
    }

    private static bool AlternativeTitlesNeedFill(Film film, bool refillEmpty) =>
        film.AlternativeTitles is null ||
        (refillEmpty && film.AlternativeTitles.Count == 0);

    private static bool HasServiceImage(IReadOnlyDictionary<string, ServiceLink>? services) =>
        services?.Values.Any(link => link?.Image is not null) == true;
}
