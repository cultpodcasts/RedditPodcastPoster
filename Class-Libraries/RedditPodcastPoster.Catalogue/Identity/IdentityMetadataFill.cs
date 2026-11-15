using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Catalogue.Identity;

/// <summary>
/// Fills a film or TV episode from its identity (TMDB) metadata, only where the field is empty (RPP #1430, #1431).
/// A value that is already set, by a streaming service or a curator, is never overwritten.
/// </summary>
public static class IdentityMetadataFill
{
    /// <summary>
    /// True when the film has an empty field that identity metadata can fill: description, release, runtime,
    /// image (when no streaming service has one either) or alternative titles (never set; a cleared list is kept).
    /// </summary>
    public static bool HasGaps(Film film)
    {
        ArgumentNullException.ThrowIfNull(film);
        return string.IsNullOrWhiteSpace(film.Description) ||
               film.Release is null ||
               film.Length <= TimeSpan.Zero ||
               (film.Image is null && !HasServiceImage(film.Services)) ||
               film.AlternativeTitles is null;
    }

    /// <summary>
    /// True when the episode has an empty field that identity metadata can fill: name, overview, air date, runtime or
    /// image (when no streaming service has one either).
    /// </summary>
    public static bool HasGaps(TvShowEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return string.IsNullOrWhiteSpace(episode.Title) ||
               string.IsNullOrWhiteSpace(episode.Description) ||
               episode.Release is null ||
               episode.Length <= TimeSpan.Zero ||
               (episode.Image is null && !HasServiceImage(episode.Services));
    }

    /// <summary>Fills the film's empty fields from <paramref name="metadata"/>. True when anything changed.</summary>
    public static bool Fill(Film film, TmdbMovieMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(film);
        ArgumentNullException.ThrowIfNull(metadata);
        var changed = false;
        if (film.TmdbId is null && metadata.Id > 0)
        {
            film.TmdbId = metadata.Id;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(film.Description) && metadata.Overview is { } overview)
        {
            film.Description = overview;
            changed = true;
        }

        if (film.Release is null && metadata.ReleaseDate is { } released)
        {
            film.SetRelease(CatalogueRelease.FromDate(released));
            changed = true;
        }

        if (film.Length <= TimeSpan.Zero && metadata.RuntimeMinutes is int minutes and > 0)
        {
            film.Length = TimeSpan.FromMinutes(minutes);
            changed = true;
        }

        if (film.Image is null && metadata.Image is { } image)
        {
            film.Image = image;
            changed = true;
        }

        // Null means never set. [] means a curator cleared them, so it is not filled again.
        if (film.AlternativeTitles is null)
        {
            var titles = metadata.OtherTitles
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title.Trim())
                .Where(title => !string.Equals(title, film.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (titles.Count > 0)
            {
                film.AlternativeTitles = titles;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>Fills the episode's empty fields from <paramref name="metadata"/>. True when anything changed.</summary>
    public static bool Fill(TvShowEpisode episode, TmdbEpisodeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(metadata);
        var changed = false;
        if (episode.TmdbId is null && metadata.Id > 0)
        {
            episode.TmdbId = metadata.Id;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(episode.Title) && metadata.Name is { } name)
        {
            episode.Title = name;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(episode.Description) && metadata.Overview is { } overview)
        {
            episode.Description = overview;
            changed = true;
        }

        if (episode.Release is null && metadata.AirDate is { } aired)
        {
            episode.SetRelease(CatalogueRelease.FromDate(aired));
            changed = true;
        }

        if (episode.Length <= TimeSpan.Zero && metadata.RuntimeMinutes is int minutes and > 0)
        {
            episode.Length = TimeSpan.FromMinutes(minutes);
            changed = true;
        }

        if (episode.Image is null && metadata.Still is { } still)
        {
            episode.Image = still;
            changed = true;
        }

        return changed;
    }

    private static bool HasServiceImage(IReadOnlyDictionary<string, ServiceLink>? services) =>
        services?.Values.Any(link => link?.Image is not null) == true;
}
