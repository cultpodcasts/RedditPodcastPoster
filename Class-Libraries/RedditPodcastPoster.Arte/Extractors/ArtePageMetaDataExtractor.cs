using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Arte.Matching;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Arte.Extractors;

public interface IArtePageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class ArtePageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : IArtePageMetaDataExtractor
{
    public const string Publisher = "ARTE";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var client = httpClientFactory.CreateClient(nameof(ArtePageMetaDataExtractor));
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
            // Soft-walled shells often omit og:title; fall through to the document title.
        }

        return ArteCatalogMeta.Merge(url, html, openGraph);
    }
}

internal static partial class ArteCatalogMeta
{
    public static NonPodcastServiceItemMetaData Merge(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph)
    {
        var cleaned = CleanTitle(openGraph?.Title ?? FirstGroup(html, DocumentTitleRegex()));
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "ARTE page has neither og:title nor a usable document title.");
        }

        string title;
        string? showName;
        if (ArteUrlMatcher.IsSeriesUrl(url))
        {
            title = BrandBeforeDash(cleaned) ?? cleaned;
            showName = title;
        }
        else if (HasParentCollection(html))
        {
            title = cleaned;
            showName = BrandBeforeDash(cleaned);
        }
        else
        {
            title = cleaned;
            showName = null;
        }

        if (string.Equals(showName, ArtePageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase))
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
            ArtePageMetaDataExtractor.Publisher,
            showName);
    }

    /// <summary>
    /// Player tracking embeds <c>associatedCollections":["RC-…"]</c> (often JSON-escaped inside the Next.js payload).
    /// An empty array is a one-off film or standalone programme.
    /// </summary>
    public static bool HasParentCollection(string html)
    {
        var match = AssociatedCollectionsRegex().Match(html);
        return match.Success &&
               match.Groups[1].Value.Contains("RC-", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BrandBeforeDash(string title)
    {
        var dash = title.IndexOf(" - ", StringComparison.Ordinal);
        var brand = (dash > 0 ? title[..dash] : title).Trim();
        if (string.IsNullOrWhiteSpace(brand) ||
            brand.Equals(ArtePageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return brand;
    }

    private static string CleanTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var title = WebUtility.HtmlDecode(raw).Trim();
        title = ArteBrandSuffixRegex().Replace(title, string.Empty).Trim();
        title = WatchCallToActionRegex().Replace(title, string.Empty).Trim();
        return title;
    }

    private static string? FirstGroup(string html, Regex regex)
    {
        var match = regex.Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
    }

    [GeneratedRegex("<title>([^<]*)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentTitleRegex();

    [GeneratedRegex(@"\s*\|\s*ARTE\b.*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ArteBrandSuffixRegex();

    /// <summary>
    /// Trailing watch CTA in the catalogue languages ARTE publishes
    /// (fr, de, en, es, pl, it, ro).
    /// </summary>
    [GeneratedRegex(
        @"\s+-\s+(?:Watch the full\b|Regarder (?:le|la|les)\b|Die ganze\b|Den ganzen\b|Ver (?:el|la|los|las)\b|Obejrzyj\b|Guarda (?:il|lo|la|l')\b|Vizioneaz\w*\b).*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WatchCallToActionRegex();

    [GeneratedRegex(
        @"associatedCollections\\"":\s*\[([^\]]*)\]",
        RegexOptions.CultureInvariant)]
    private static partial Regex AssociatedCollectionsRegex();
}
