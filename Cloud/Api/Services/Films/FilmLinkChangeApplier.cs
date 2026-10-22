using System.Text.RegularExpressions;
using Api.Models;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace Api.Services.Films;

/// <summary>
/// The film PATCH fields that identify and link a film: <c>imdbId</c>, <c>tmdbId</c>, <c>services</c> and
/// <c>trailers</c>. <see cref="TryValidate"/> runs before anything is applied, so a 400 leaves the film untouched.
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

        if (change.Services is not null)
        {
            foreach (var pair in change.Services)
            {
                var url = Blank(pair.Value?.Url);
                var image = Blank(pair.Value?.Image);
                if (url is not null && !IsAbsoluteHttp(url))
                {
                    error = $"services.{pair.Key}.url must be an absolute http(s) URL";
                    return false;
                }

                if (image is not null && !IsAbsoluteHttp(image))
                {
                    error = $"services.{pair.Key}.image must be an absolute http(s) URL";
                    return false;
                }
            }
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
            ApplyServices(film, change.Services);
        }

        if (change.Trailers is not null)
        {
            ApplyTrailers(film, change.Trailers);
        }
    }

    /// <summary>Same semantics as the episode edit API's <c>services</c> patch.</summary>
    private static void ApplyServices(Film film, Dictionary<string, ServiceLink> services)
    {
        foreach (var pair in services)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            var url = Blank(pair.Value?.Url);
            var image = Blank(pair.Value?.Image);
            if (url is null && image is null)
            {
                film.Services?.Remove(pair.Key);
                continue;
            }

            var key = url is null ? pair.Key : StreamingServiceCatalog.TryResolveKey(url) ?? pair.Key;
            film.Services ??= new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
            if (!film.Services.TryGetValue(key, out var link))
            {
                link = new ServiceLink();
                film.Services[key] = link;
            }

            if (url is not null)
            {
                link.Url = url;
            }

            if (image is not null)
            {
                link.Image = image;
            }
        }

        if (film.Services is { Count: 0 })
        {
            film.Services = null;
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

    private static Uri? Blank(Uri? uri) => uri is null || uri.ToString().Length == 0 ? null : uri;

    private static bool IsAbsoluteHttp(Uri uri) =>
        uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
