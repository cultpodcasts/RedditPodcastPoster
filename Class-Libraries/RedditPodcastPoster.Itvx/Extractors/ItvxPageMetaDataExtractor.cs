using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Itvx.Matching;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Itvx.Extractors;

public interface IItvxPageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class ItvxPageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : IItvxPageMetaDataExtractor
{
    public const string Publisher = "ITVX";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var client = httpClientFactory.CreateClient(nameof(ItvxPageMetaDataExtractor));
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

        return ItvxCatalogMeta.Merge(url, html, openGraph);
    }
}

internal static partial class ItvxCatalogMeta
{
    public static NonPodcastServiceItemMetaData Merge(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph)
    {
        var title = CleanTitle(openGraph?.Title ?? FirstGroup(html, DocumentTitleRegex()));
        if (IsUnusableTitle(title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "ITVX page has neither og:title nor a usable document title. Geo/login walls often reset the connection or return the homepage shell.");
        }

        var showName = openGraph?.ShowName;
        if (IsMovie(url, html))
        {
            showName = null;
        }
        else
        {
            showName ??= FirstGroup(html, TvSeriesNameRegex());
            // Brand/programme hubs only: title ≈ show. Episode watch paths use
            // og:title for the episode, so title-as-ShowName would poison podcastName.
            if (showName is null && ItvxUrlMatcher.IsWatchBrandHubPath(url))
            {
                showName = title;
            }
        }

        if (string.Equals(showName, ItvxPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase))
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
            ItvxPageMetaDataExtractor.Publisher,
            showName);
    }

    /// <summary>
    /// True when og:type is a movie, the URL is a film catalogue path, or the
    /// <em>primary</em> catalogue <c>@type</c> is Movie. Series evidence
    /// (TVSeries name blob or generic series path) wins over carousel Movie
    /// JSON-LD so recommended films cannot null ShowName on series pages.
    /// Watch catalogue paths without TVSeries still classify via primary
    /// <c>@type=Movie</c> (films need not rely solely on og:type).
    /// </summary>
    public static bool IsMovie(Uri url, string html)
    {
        var ogType = FirstGroup(html, OgTypeRegex());
        if (string.Equals(ogType, "video.movie", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ogType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Series evidence beats document-wide / earlier carousel Movie blobs.
        if (StreamingCataloguePathHints.IsSeriesPath(url) ||
            FirstGroup(html, TvSeriesNameRegex()) is not null)
        {
            return false;
        }

        if (StreamingCataloguePathHints.IsMoviePath(url))
        {
            return true;
        }

        var catalogue = CataloguePrimaryTypeRegex().Match(html);
        return catalogue.Success &&
               catalogue.Groups[1].Value.Equals("Movie", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnusableTitle(string title) =>
        string.IsNullOrWhiteSpace(title) ||
        title.Equals("ITVX Homepage", StringComparison.OrdinalIgnoreCase) ||
        title.Equals(ItvxPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase);

    private static string CleanTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var title = WebUtility.HtmlDecode(raw).Trim();
        foreach (var suffix in new[] { " - Watch Episode - ITVX", " | ITVX", " - ITVX" })
        {
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                title = title[..^suffix.Length].Trim();
            }
        }

        return title;
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

    [GeneratedRegex("\"@type\"\\s*:\\s*\"(TVSeries|Movie)\"", RegexOptions.CultureInvariant)]
    private static partial Regex CataloguePrimaryTypeRegex();

    [GeneratedRegex(
        "\"@type\"\\s*:\\s*\"TVSeries\"[\\s\\S]{0,400}?\"name\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex TvSeriesNameRegex();
}
