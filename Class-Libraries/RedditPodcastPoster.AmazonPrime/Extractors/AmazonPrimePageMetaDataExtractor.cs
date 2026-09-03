using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.AmazonPrime.Extractors;

public interface IAmazonPrimePageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class AmazonPrimePageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : IAmazonPrimePageMetaDataExtractor
{
    private const string Publisher = "Amazon Prime Video";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var client = httpClientFactory.CreateClient(nameof(AmazonPrimePageMetaDataExtractor));
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
            // Live Prime pages often omit og:title; fall through to ATV/title-tag parsing.
        }

        return AmazonPrimeAtvMeta.Merge(url, html, openGraph);
    }
}

internal static partial class AmazonPrimeAtvMeta
{
    public static NonPodcastServiceItemMetaData Merge(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph)
    {
        var title = openGraph?.Title
                    ?? CleanTitle(MetaNameTitle(html) ?? DocumentTitle(html));
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "Prime Video page has neither og:title nor a usable <title>/meta title.");
        }

        var showName = openGraph?.ShowName;
        if (IsMovie(html))
        {
            showName = null;
        }
        else
        {
            showName ??= TrySeriesName(html);
        }

        return new NonPodcastServiceItemMetaData(
            title,
            openGraph?.Description ?? string.Empty,
            openGraph?.Duration,
            openGraph?.Release,
            openGraph?.Image,
            openGraph?.Explicit,
            "Amazon Prime Video",
            showName);
    }

    public static string? TrySeriesName(string html)
    {
        if (IsMovie(html))
        {
            return null;
        }

        // Prefer parentTitle co-located with season / TV Show markers so carousel
        // related-title blobs earlier in the HTML cannot win.
        var parentTitle =
            FirstGroup(html, TitleTypeThenParentTitleRegex())
            ?? FirstGroup(html, ParentTitleThenTitleTypeRegex())
            ?? FirstGroup(html, EntityTypeThenParentTitleRegex())
            ?? FirstGroup(html, ParentTitleThenEntityTypeRegex())
            ?? FirstGroup(html, ParentTitleRegex());
        return string.IsNullOrWhiteSpace(parentTitle) ? null : parentTitle.Trim();
    }

    public static bool IsMovie(string html)
    {
        var titleType = FirstGroup(html, TitleTypeRegex());
        if (string.Equals(titleType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var entityType = FirstGroup(html, EntityTypeRegex());
        return string.Equals(entityType, "Movie", StringComparison.OrdinalIgnoreCase);
    }

    private static string? MetaNameTitle(string html) =>
        FirstGroup(html, MetaNameTitleRegex()) ?? FirstGroup(html, MetaNameTitleReverseRegex());

    private static string? DocumentTitle(string html) => FirstGroup(html, DocumentTitleRegex());

    private static string CleanTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var title = raw.Trim();
        const string primePrefix = "Prime Video:";
        if (title.StartsWith(primePrefix, StringComparison.OrdinalIgnoreCase))
        {
            title = title[primePrefix.Length..].Trim();
        }

        const string watchPrefix = "Watch ";
        if (title.StartsWith(watchPrefix, StringComparison.OrdinalIgnoreCase))
        {
            title = title[watchPrefix.Length..].Trim();
        }

        const string primeSuffix = " - Prime Video";
        if (title.EndsWith(primeSuffix, StringComparison.OrdinalIgnoreCase))
        {
            title = title[..^primeSuffix.Length].Trim();
        }

        return title;
    }

    private static string? FirstGroup(string html, Regex regex)
    {
        var match = regex.Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex("\"parentTitle\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ParentTitleRegex();

    [GeneratedRegex(
        "\"titleType\"\\s*:\\s*\"(?:season|series)\"[\\s\\S]{0,800}?\"parentTitle\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TitleTypeThenParentTitleRegex();

    [GeneratedRegex(
        "\"parentTitle\"\\s*:\\s*\"([^\"]+)\"[\\s\\S]{0,800}?\"titleType\"\\s*:\\s*\"(?:season|series)\"",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ParentTitleThenTitleTypeRegex();

    [GeneratedRegex(
        "\"entityType\"\\s*:\\s*\"TV Show\"[\\s\\S]{0,800}?\"parentTitle\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex EntityTypeThenParentTitleRegex();

    [GeneratedRegex(
        "\"parentTitle\"\\s*:\\s*\"([^\"]+)\"[\\s\\S]{0,800}?\"entityType\"\\s*:\\s*\"TV Show\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex ParentTitleThenEntityTypeRegex();

    [GeneratedRegex("\"titleType\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex TitleTypeRegex();

    [GeneratedRegex("\"entityType\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex EntityTypeRegex();

    [GeneratedRegex("<title>([^<]*)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentTitleRegex();

    [GeneratedRegex("name=\"title\"[^>]*content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaNameTitleRegex();

    [GeneratedRegex("content=\"([^\"]*)\"[^>]*name=\"title\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaNameTitleReverseRegex();
}
