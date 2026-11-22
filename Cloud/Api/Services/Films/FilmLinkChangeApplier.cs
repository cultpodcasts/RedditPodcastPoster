using System.Text.RegularExpressions;
using Api.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.YouTube;
using RedditPodcastPoster.Models.Podcasts;

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

        if (change.AdditionalYouTube is not null)
        {
            ApplyYouTubeAlternates(film, change.AdditionalYouTube);
        }

        if (!string.IsNullOrWhiteSpace(change.PromoteYouTubeId))
        {
            PromoteYouTube(film, change.PromoteYouTubeId.Trim(), DateTime.UtcNow);
        }

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
        error = null;
        var primary = PrimaryYouTubeId(film);
        var trailerIds = TrailerYouTubeIds(film);
        var promote = string.IsNullOrWhiteSpace(change.PromoteYouTubeId) ? null : change.PromoteYouTubeId.Trim();
        HashSet<string>? resultingIds = null;

        if (change.AdditionalYouTube is not null)
        {
            resultingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in change.AdditionalYouTube)
            {
                if (!TryResolveChange(film, entry, out var youTubeId, out error))
                {
                    return false;
                }

                if (!resultingIds.Add(youTubeId))
                {
                    error = $"additional YouTube '{youTubeId}' is listed twice";
                    return false;
                }

                if (string.Equals(youTubeId, primary, StringComparison.Ordinal))
                {
                    error = $"additional YouTube '{youTubeId}' is already the film's primary YouTube link";
                    return false;
                }

                if (trailerIds.Contains(youTubeId))
                {
                    error = $"additional YouTube '{youTubeId}' is already a trailer on this film";
                    return false;
                }
            }
        }

        if (promote is not null)
        {
            if (resultingIds is not null)
            {
                if (!resultingIds.Contains(promote))
                {
                    error = $"promoteYouTubeId '{promote}' is not in additionalYouTube";
                    return false;
                }
            }
            else if (!YouTubeAlternates.Holds(film.AdditionalYouTube, promote))
            {
                error = $"the film has no additional YouTube '{promote}'";
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveChange(
        Film film,
        YouTubeAlternateChange entry,
        out string youTubeId,
        out string? error)
    {
        youTubeId = string.Empty;
        error = null;
        var listedId = string.IsNullOrWhiteSpace(entry.YouTubeId) ? null : entry.YouTubeId.Trim();
        if (listedId is not null)
        {
            if (YouTubeAlternates.Holds(film.AdditionalYouTube, listedId))
            {
                youTubeId = listedId;
                return true;
            }

            if (string.IsNullOrWhiteSpace(entry.Url))
            {
                error = $"the film has no additional YouTube '{listedId}'";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(entry.Url) || !Uri.TryCreate(entry.Url, UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            error = "additional YouTube url must be an absolute http(s) YouTube URL";
            return false;
        }

        var extracted = YouTubeIdResolver.Extract(url);
        if (string.IsNullOrWhiteSpace(extracted))
        {
            error = "additional YouTube url must be a YouTube video URL";
            return false;
        }

        if (listedId is not null && !string.Equals(listedId, extracted, StringComparison.Ordinal))
        {
            error = $"additional YouTube youTubeId '{listedId}' does not match url";
            return false;
        }

        youTubeId = extracted;
        return true;
    }

    private static void ApplyYouTubeAlternates(Film film, YouTubeAlternateChange[] keep)
    {
        var current = film.AdditionalYouTube ?? [];
        var kept = new List<YouTubeAlternate>(keep.Length);
        var now = DateTime.UtcNow;
        foreach (var change in keep)
        {
            var existingId = string.IsNullOrWhiteSpace(change.YouTubeId) ? null : change.YouTubeId.Trim();
            var existing = existingId is null
                ? null
                : current.FirstOrDefault(item => string.Equals(item.YouTubeId, existingId, StringComparison.Ordinal));
            if (existing is not null)
            {
                if (!string.IsNullOrWhiteSpace(change.Title))
                {
                    existing.Title = change.Title.Trim();
                }

                if (!string.IsNullOrWhiteSpace(change.ChannelName))
                {
                    existing.ChannelName = change.ChannelName.Trim();
                }

                kept.Add(existing);
                continue;
            }

            if (!Uri.TryCreate(change.Url, UriKind.Absolute, out var url))
            {
                continue;
            }

            var created = YouTubeAlternates.FromUrl(url, now);
            if (created is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(change.Title))
            {
                created.Title = change.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(change.ChannelName))
            {
                created.ChannelName = change.ChannelName.Trim();
            }

            kept.Add(created);
        }

        film.AdditionalYouTube = YouTubeAlternates.OrNull(kept);
    }

    private static void PromoteYouTube(Film film, string youTubeId, DateTime nowUtc)
    {
        var current = film.AdditionalYouTube;
        var promoted = current?.FirstOrDefault(item =>
            string.Equals(item.YouTubeId, youTubeId, StringComparison.Ordinal));
        if (promoted is null)
        {
            return;
        }

        var previous = YouTubeAlternates.FromPrimary(film.Services, film.YouTubeId, nowUtc);
        film.Services ??= new Dictionary<string, ServiceLink>();
        film.Services[ServiceKeys.YouTube] = new ServiceLink
        {
            Url = promoted.Url,
            Image = promoted.Image
        };
        EpisodeServicePresence.SyncCatalogueYouTubeIdFromUrl(film);

        var remaining = (current ?? [])
            .Where(item => !string.Equals(item.YouTubeId, youTubeId, StringComparison.Ordinal))
            .ToList();
        if (previous is not null && !YouTubeAlternates.Holds(remaining, previous.YouTubeId))
        {
            remaining.Insert(0, previous);
        }

        film.AdditionalYouTube = YouTubeAlternates.OrNull(remaining);
    }

    internal static string? PrimaryYouTubeId(Film film)
    {
        if (!string.IsNullOrWhiteSpace(film.YouTubeId))
        {
            return film.YouTubeId;
        }

        if (film.Services is not null &&
            film.Services.TryGetValue(ServiceKeys.YouTube, out var link) &&
            link?.Url is not null)
        {
            return YouTubeIdResolver.Extract(link.Url);
        }

        return null;
    }

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
