using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Netflix.Matching;

public static partial class NetflixUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "netflix.com"))
        {
            return false;
        }

        var path = url.AbsolutePath;
        return path.Contains("/title/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/watch/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The Netflix title id for <c>/title/{id}</c>, with or without a country/locale segment
    /// (<c>/us/title/{id}</c>, <c>/gb/title/{id}</c>, <c>/gb-en/title/{id}</c>). #1517: every form compacts to the
    /// same id, so the stored URL is always <c>https://www.netflix.com/title/{id}</c> and one title cannot be
    /// stored twice.
    /// </summary>
    public static string? TryCompactPayload(Uri url) =>
        StreamingUrlCodecs.TryTrimPrefixHostPath(WithoutCountry(url), ["/title/"], allowSlug: false, hosts: ["netflix.com"]);

    /// <summary>The canonical stored URL for a Netflix title page, or the URL unchanged when it is not one.</summary>
    public static Uri CanonicalUrl(Uri url)
    {
        var compact = TryCompactPayload(url);
        return compact is null ? url : TryExpandPayload(compact) ?? url;
    }

    private static Uri WithoutCountry(Uri url)
    {
        if (!url.IsAbsoluteUri)
        {
            return url;
        }

        var match = CountryPrefix().Match(url.AbsolutePath);
        if (!match.Success)
        {
            return url;
        }

        var builder = new UriBuilder(url) { Path = url.AbsolutePath[match.Length..] };
        return builder.Uri;
    }

    [GeneratedRegex("^/[a-z]{2}(-[a-z]{2})?(?=/title/)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CountryPrefix();

    public static Uri? TryExpandPayload(string payload) =>
        StreamingUrlCodecs.TryCreate($"https://www.netflix.com/title/{payload}");
}
