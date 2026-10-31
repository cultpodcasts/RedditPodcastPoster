using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.CbcGem.Matching;

/// <summary>
/// CBC Gem catalogue URLs.
/// <list type="bullet">
/// <item><c>/{slug}</c>: a film or a series hub (the page JSON-LD decides which; the extractor rejects hubs).</item>
/// <item><c>/{slug}/s{NN}e{NN}</c>: a series episode.</item>
/// </list>
/// Season hubs (<c>/{slug}/s01</c>), app shells (<c>/live</c>, <c>/search</c>, …) and deeper paths are not submit URLs.
/// The stored form is <c>https://gem.cbc.ca/{slug}[/sNNeNN]</c>, lower-case, with no query or fragment.
/// </summary>
public static partial class CbcGemUrlMatcher
{
    public const string Host = "gem.cbc.ca";

    private static readonly HashSet<string> NonCatalogueSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "live", "category", "categories", "section", "sections", "collection", "collections",
        "browse", "search", "account", "settings", "login", "logout", "signin", "signup",
        "subscribe", "premium", "my-list", "mylist", "schedule", "faq", "help", "about",
        "terms", "privacy", "movies", "shows", "kids", "news", "media", "player", "watch"
    };

    public static bool IsSubmitUrl(Uri url) => TryCompactPayload(url) is not null;

    /// <summary>
    /// True for <c>/{slug}/s{NN}e{NN}</c> episode pages.
    /// </summary>
    public static bool IsEpisodeUrl(Uri url) =>
        TryCompactPayload(url) is { } payload && payload.Contains('/');

    public static Uri CanonicalUrl(Uri url)
    {
        var compact = TryCompactPayload(url);
        return compact is null ? url : TryExpandPayload(compact) ?? url;
    }

    public static string? TryCompactPayload(Uri url)
    {
        if (!url.IsAbsoluteUri ||
            (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp) ||
            !ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), Host))
        {
            return null;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2 ||
            !SlugRegex().IsMatch(parts[0]) ||
            NonCatalogueSegments.Contains(parts[0]))
        {
            return null;
        }

        var slug = parts[0].ToLowerInvariant();
        if (parts.Length == 1)
        {
            return slug;
        }

        return EpisodeSegmentRegex().IsMatch(parts[1])
            ? $"{slug}/{parts[1].ToLowerInvariant()}"
            : null;
    }

    public static Uri? TryExpandPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var candidate = StreamingUrlCodecs.TryCreate($"https://{Host}/{payload}");
        return candidate is not null && TryCompactPayload(candidate) == payload ? candidate : null;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();

    [GeneratedRegex("^s\\d{1,3}e\\d{1,4}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeSegmentRegex();
}
