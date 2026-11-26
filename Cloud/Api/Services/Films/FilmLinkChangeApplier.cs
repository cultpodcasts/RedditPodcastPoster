using System.Text.RegularExpressions;
using Api.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.YouTube;
using RedditPodcastPoster.Models.Podcasts;
using Api.Services;

namespace Api.Services.Films;

/// <summary>
/// The film PATCH fields that identify and link a film: <c>imdbId</c>, <c>tmdbId</c>, <c>services</c>,
/// <c>trailers</c> and <c>alternativeTitles</c>. <see cref="TryValidate"/> runs before anything is applied, so a 400 leaves the film untouched.
/// </summary>
internal static partial class FilmLinkChangeApplier
{
    [GeneratedRegex(@"^tt\d{7,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex ImdbIdPattern();

    public static bool TryValidate(Film film, FilmChangeRequest change, out string? error)
    {
        error = null;
        if (change.ImdbId is { } imdbId && imdbId.Trim().Length > 0 && !ImdbIdPattern().IsMatch(imdbId.Trim()))
        {
            error = "imdbId must be 'tt' followed by 7 to 10 digits";
            return false;
        }

        if (change.TmdbId is < 0)
        {
            error = "tmdbId must be positive (0 clears it)";
            return false;
        }

        if (change.Services is not null &&
            !Catalogue.PlayableServicesChangeApplier.TryValidate(change.Services, out error))
        {
            return false;
        }

        if (change.Trailers is not null)
        {
            var seen = new HashSet<Guid>();
            foreach (var trailer in change.Trailers)
            {
                if (!seen.Add(trailer.Id))
                {
                    error = $"trailer '{trailer.Id}' is listed twice";
                    return false;
                }

                if (!film.HasTrailer(trailer.Id))
                {
                    error = $"the film has no trailer '{trailer.Id}'";
                    return false;
                }
            }
        }

        if (!TryValidateYouTubeAlternates(film, change, out error))
        {
            return false;
        }

        return true;
    }

    public static void Apply(Film film, FilmChangeRequest change)
    {
        if (change.ImdbId is not null)
        {
            film.ImdbId = change.ImdbId.Trim().Length == 0 ? null : change.ImdbId.Trim();
        }

        if (change.TmdbId is not null)
        {
            film.TmdbId = change.TmdbId.Value == 0 ? null : change.TmdbId.Value;
        }

        if (change.Services is not null)
        {
            film.Services = Catalogue.PlayableServicesChangeApplier.Apply(film.Services, change.Services);
            EpisodeServicePresence.SyncCatalogueYouTubeId(film);
        }

        if (change.Trailers is not null)
        {
            ApplyTrailers(film, change.Trailers);
        }

        YouTubeAlternateChangeApplier.Apply(film, change.AdditionalYouTube, change.PromoteYouTubeId);

        if (change.AlternativeTitles is not null)
        {
            var titles = change.AlternativeTitles
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title.Trim())
                .Where(title => !string.Equals(title, film.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            // [] is kept: the curator cleared them, so identity enrichment does not fill them again.
            film.AlternativeTitles = titles;
        }
    }

    private static void ApplyTrailers(Film film, FilmTrailerChange[] keep)
    {
        var current = film.Trailers ?? [];
        var kept = new List<FilmTrailer>(keep.Length);
        foreach (var change in keep)
        {
            var trailer = current.First(x => x.Id == change.Id);
            if (!string.IsNullOrWhiteSpace(change.Title))
            {
                trailer.Title = change.Title.Trim();
            }

            kept.Add(trailer);
        }

        film.Trailers = kept.Count == 0 ? null : kept;
    }

    internal static bool TryValidateYouTubeAlternates(Film film, FilmChangeRequest change, out string? error)
    {
        var trailerIds = TrailerYouTubeIds(film);
        return YouTubeAlternateChangeApplier.TryValidate(
            film,
            change.AdditionalYouTube,
            change.PromoteYouTubeId,
            ResultingPrimaryYouTubeId(film, change),
            "film",
            youTubeId => trailerIds.Contains(youTubeId)
                ? $"additional YouTube '{youTubeId}' is already a trailer on this film"
                : null,
            out error);
    }

    /// <summary>
    /// The YouTube id the film will have as its primary after <see cref="Apply"/> runs <c>services</c>
    /// (RPP #1411 review). Validation that rejects an additionalYouTube entry matching the primary must
    /// use this, not the pre-PATCH primary, so a PATCH that moves the primary to X and also lists X as
    /// an alternate is rejected.
    /// </summary>
    internal static string? ResultingPrimaryYouTubeId(Film film, FilmChangeRequest change) =>
        YouTubeAlternateChangeApplier.ResultingPrimaryYouTubeId(film, change.Services);

    internal static HashSet<string> TrailerYouTubeIds(Film film)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trailer in film.Trailers ?? [])
        {
            if (!string.IsNullOrWhiteSpace(trailer.Ids?.YouTube))
            {
                ids.Add(trailer.Ids.YouTube);
            }
            else if (trailer.Url is not null)
            {
                var id = YouTubeIdResolver.Extract(trailer.Url);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }
}
