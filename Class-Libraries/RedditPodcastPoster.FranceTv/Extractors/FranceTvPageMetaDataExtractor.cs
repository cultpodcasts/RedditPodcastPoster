using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RedditPodcastPoster.FranceTv.Matching;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.FranceTv.Extractors;

public interface IFranceTvPageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class FranceTvPageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : IFranceTvPageMetaDataExtractor
{
    public const string Publisher = "France TV";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var client = httpClientFactory.CreateClient(nameof(FranceTvPageMetaDataExtractor));
        using var pageResponse = await client.GetAsync(url);
        if (pageResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, pageResponse.StatusCode);
        }

        var html = await pageResponse.Content.ReadAsStringAsync();
        NonPodcastServiceItemMetaData? openGraph = null;
        try
        {
            using var buffered = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            };
            openGraph = await openGraphPageMetaDataExtractor.Extract(url, buffered, Publisher);
        }
        catch (NonPodcastServiceMetaDataExtractionException)
        {
            // Soft-walled / non-catalogue shells often omit og:title; fall through to HTML recovery.
        }

        return FranceTvCatalogMeta.Merge(url, html, openGraph);
    }
}

internal static partial class FranceTvCatalogMeta
{
    public static NonPodcastServiceItemMetaData Merge(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph)
    {
        var title = CleanTitle(openGraph?.Title ?? FirstGroup(html, DocumentTitleRegex()));
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "France TV page has neither og:title nor a usable document title.");
        }

        string? showName;
        if (IsMovie(html))
        {
            showName = null;
        }
        else
        {
            showName = openGraph?.ShowName
                       ?? BreadcrumbSeriesName(html)
                       ?? (FranceTvUrlMatcher.IsSeriesUrl(url) ? title : null);
        }

        if (string.Equals(showName, FranceTvPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(showName, "france.tv", StringComparison.OrdinalIgnoreCase))
        {
            showName = null;
        }

        return new NonPodcastServiceItemMetaData(
            title,
            openGraph?.Description ?? string.Empty,
            openGraph?.Duration,
            openGraph?.Release,
            openGraph?.Image,
            openGraph?.Explicit,
            FranceTvPageMetaDataExtractor.Publisher,
            showName,
            MadeAsFilm: IsMovie(html));
    }

    public static bool IsMovie(string html)
    {
        var ogType = FirstGroup(html, OgTypeRegex());
        if (string.Equals(ogType, "video.movie", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ogType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var catalogue = CataloguePrimaryTypeRegex().Match(html);
        return catalogue.Success &&
               catalogue.Groups[1].Value.Equals("Movie", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BreadcrumbSeriesName(string html)
    {
        var list = BreadcrumbListRegex().Match(html);
        if (!list.Success)
        {
            return null;
        }

        string? lastUseful = null;
        foreach (Match nameMatch in BreadcrumbNameRegex().Matches(list.Groups[1].Value))
        {
            var name = WebUtility.HtmlDecode(nameMatch.Groups[1].Value).Trim();
            if (string.IsNullOrWhiteSpace(name) ||
                string.Equals(name, "france.tv", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, FranceTvPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase) ||
                (name.StartsWith("france", StringComparison.OrdinalIgnoreCase) &&
                 name.Contains("slash", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            lastUseful = name;
        }

        return lastUseful;
    }

    private static string CleanTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var title = WebUtility.HtmlDecode(raw).Trim();
        foreach (var suffix in new[] { " | France TV", " - France TV" })
        {
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                title = title[..^suffix.Length].Trim();
            }
        }

        // Live OG titles append SEO marketing such as
        // " - Documentaire en replay {series brand}" after "{series} - {episode}".
        title = ReplayMarketingTailRegex().Replace(title, string.Empty).Trim();
        return title;
    }

    private static string? FirstGroup(string html, Regex regex)
    {
        var match = regex.Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
    }

    [GeneratedRegex("<title>([^<]*)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentTitleRegex();

    /// <summary>
    /// Trailing France TV SEO clause: optional genre word + "en replay" + rest of string.
    /// Keeps the preceding "{series} - {episode}" (or series-only) title.
    /// </summary>
    [GeneratedRegex(
        @"\s+-\s+(?:\p{L}+\s+)?en\s+replay\b.*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReplayMarketingTailRegex();

    [GeneratedRegex("(?:property|name)=\"og:type\"[^>]*content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgTypeRegex();

    [GeneratedRegex("\"@type\"\\s*:\\s*\"(TVSeries|Movie)\"", RegexOptions.CultureInvariant)]
    private static partial Regex CataloguePrimaryTypeRegex();

    [GeneratedRegex(
        "\"@type\"\\s*:\\s*\"BreadcrumbList\"[\\s\\S]*?\"itemListElement\"\\s*:\\s*\\[([\\s\\S]*?)\\]",
        RegexOptions.CultureInvariant)]
    private static partial Regex BreadcrumbListRegex();

    [GeneratedRegex("\"name\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex BreadcrumbNameRegex();
}
