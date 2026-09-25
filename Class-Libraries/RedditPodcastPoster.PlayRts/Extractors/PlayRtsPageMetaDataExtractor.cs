using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PlayRts.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Models; // pragma: allowlist secret

namespace RedditPodcastPoster.PlayRts.Extractors;

public interface IPlayRtsPageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url); // pragma: allowlist secret
}

public class PlayRtsPageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : IPlayRtsPageMetaDataExtractor
{
    public const string Publisher = "Play RTS";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url) // pragma: allowlist secret
    {
        var client = httpClientFactory.CreateClient(nameof(PlayRtsPageMetaDataExtractor));
        using var pageResponse = await client.GetAsync(url);
        if (pageResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, pageResponse.StatusCode); // pragma: allowlist secret
        }

        var html = await pageResponse.Content.ReadAsStringAsync();
        NonPodcastServiceItemMetaData? openGraph = null; // pragma: allowlist secret
        try
        {
            using var buffered = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            };
            openGraph = await openGraphPageMetaDataExtractor.Extract(url, buffered, Publisher);
        }
        catch (NonPodcastServiceMetaDataExtractionException) // pragma: allowlist secret
        {
            // Soft-walled / non-catalogue shells often omit og:title; fall through to HTML recovery.
        }

        return PlayRtsCatalogMeta.Merge(url, html, openGraph);
    }
}

internal static partial class PlayRtsCatalogMeta
{
    public static NonPodcastServiceItemMetaData Merge( // pragma: allowlist secret
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph) // pragma: allowlist secret
    {
        var title = CleanTitle(
            openGraph?.JsonLdName ?? openGraph?.Title ?? FirstGroup(html, DocumentTitleRegex()));
        var showName = openGraph?.ShowName;
        title = StripShowNameSuffix(title, showName);

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new NonPodcastServiceMetaDataExtractionException( // pragma: allowlist secret
                url,
                "Play RTS page has neither og:title nor a usable document title. Geo/login walls often return a non-catalogue shell.");
        }

        if (IsMovie(url, html))
        {
            showName = null;
        }
        else if (showName is null && !PlayRtsUrlMatcher.IsEpisodePath(url))
        {
            showName = title;
        }

        if (string.Equals(showName, PlayRtsPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase))
        {
            showName = null;
        }

        return new NonPodcastServiceItemMetaData( // pragma: allowlist secret
            title,
            openGraph?.Description ?? FirstGroup(html, OgDescriptionRegex()) ?? string.Empty,
            openGraph?.Duration,
            openGraph?.Release,
            openGraph?.Image,
            openGraph?.Explicit,
            PlayRtsPageMetaDataExtractor.Publisher,
            showName,
            MadeAsFilm: IsMovie(url, html));
    }

    public static bool IsMovie(Uri url, string html)
    {
        var ogType = FirstGroup(html, OgTypeRegex());
        if (string.Equals(ogType, "video.movie", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ogType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (PlayRtsUrlMatcher.IsEpisodePath(url))
        {
            return false;
        }

        var catalogue = CataloguePrimaryTypeRegex().Match(html);
        return catalogue.Success &&
               catalogue.Groups[1].Value.Equals("Movie", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var title = WebUtility.HtmlDecode(raw).Trim();
        foreach (var suffix in new[]
        {
            " | Play RTS",
            " - Play RTS",
        })
        {
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                title = title[..^suffix.Length].Trim();
            }
        }

        return title;
    }

    private static string StripShowNameSuffix(string title, string? showName)
    {
        if (string.IsNullOrWhiteSpace(showName))
        {
            return title;
        }

        var suffix = " - " + showName;
        return title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? title[..^suffix.Length].Trim()
            : title;
    }

    private static string? FirstGroup(string html, Regex regex)
    {
        var match = regex.Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
    }

    [GeneratedRegex("<title>([^<]*)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentTitleRegex();

    [GeneratedRegex("(?:property|name)=\"og:type\"[^>]*content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgTypeRegex();

    [GeneratedRegex("(?:property|name)=\"og:description\"[^>]*content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgDescriptionRegex();

    [GeneratedRegex("\"@type\"\\s*:\\s*\"(TVSeries|Movie)\"", RegexOptions.CultureInvariant)]
    private static partial Regex CataloguePrimaryTypeRegex();
}
