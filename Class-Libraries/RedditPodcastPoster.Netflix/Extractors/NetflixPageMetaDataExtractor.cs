using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Netflix.Extractors;

public interface INetflixPageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class NetflixPageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : INetflixPageMetaDataExtractor
{
    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var client = httpClientFactory.CreateClient(nameof(NetflixPageMetaDataExtractor));
        using var pageResponse = await client.GetAsync(url);
        if (pageResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, pageResponse.StatusCode);
        }

        var html = await pageResponse.Content.ReadAsStringAsync();
        using var buffered = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

        NonPodcastServiceItemMetaData? meta = null;
        try
        {
            meta = await openGraphPageMetaDataExtractor.Extract(url, buffered, "Netflix");
        }
        catch (NonPodcastServiceMetaDataExtractionException)
        {
            // Soft-walled / non-member title pages often omit og:title; fall through.
        }

        return NetflixCatalogMeta.Merge(url, html, meta);
    }
}

internal static partial class NetflixCatalogMeta
{
    public static NonPodcastServiceItemMetaData Merge(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph)
    {
        if (IsMovie(html))
        {
            var filmTitle = openGraph?.Title
                            ?? TryH1(html)
                            ?? throw MissingTitle(url);
            return new NonPodcastServiceItemMetaData(
                CleanTitle(filmTitle),
                openGraph?.Description ?? string.Empty,
                openGraph?.Duration,
                openGraph?.Release,
                openGraph?.Image,
                openGraph?.Explicit,
                "Netflix",
                ShowName: null);
        }

        var showName = openGraph?.ShowName;
        if (!string.IsNullOrWhiteSpace(showName))
        {
            if (IsCatalogueMarketingTitle(openGraph!.Title) &&
                openGraph.Title.IndexOf(showName, StringComparison.OrdinalIgnoreCase) < 0)
            {
                showName = null;
            }
        }

        showName ??= TrySeriesName(html, openGraph?.Title);
        showName ??= TrySoftWallShowName(html);

        var title = openGraph?.Title ?? showName ?? TryH1(html);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw MissingTitle(url);
        }

        return new NonPodcastServiceItemMetaData(
            CleanTitle(title),
            openGraph?.Description ?? string.Empty,
            openGraph?.Duration,
            openGraph?.Release,
            openGraph?.Image,
            openGraph?.Explicit,
            "Netflix",
            showName);
    }

    /// <summary>
    /// True when the <em>primary</em> soft-wall <c>type</c> or catalogue <c>@type</c> is Movie.
    /// Whole-document Movie matches are ignored so recommended/carousel film blobs cannot
    /// null <c>ShowName</c> on series catalogue pages.
    /// </summary>
    public static bool IsMovie(string html)
    {
        var softWall = SoftWallPrimaryTypeRegex().Match(html);
        if (softWall.Success)
        {
            return softWall.Groups[1].Value.Equals("Movie", StringComparison.OrdinalIgnoreCase);
        }

        var catalogue = CataloguePrimaryTypeRegex().Match(html);
        return catalogue.Success &&
               catalogue.Groups[1].Value.Equals("Movie", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCatalogueMarketingTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title) &&
        title.StartsWith("Watch ", StringComparison.OrdinalIgnoreCase) &&
        title.Contains("Netflix", StringComparison.OrdinalIgnoreCase);

    public static string? TrySeriesName(string html, string? pageTitle)
    {
        if (IsMovie(html))
        {
            return null;
        }

        var candidate = FirstSeriesCandidate(html);
        if (candidate is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(pageTitle) &&
            pageTitle.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) < 0 &&
            IsCatalogueMarketingTitle(pageTitle))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(pageTitle) &&
            IsCatalogueMarketingTitle(pageTitle) &&
            pageTitle.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return candidate;
        }

        if (string.IsNullOrWhiteSpace(pageTitle) || !IsCatalogueMarketingTitle(pageTitle))
        {
            return candidate;
        }

        return null;
    }

    private static string? TrySoftWallShowName(string html)
    {
        if (IsMovie(html) || !SoftWallShowTypeRegex().IsMatch(html))
        {
            return null;
        }

        return TryH1(html);
    }

    private static string? TryH1(string html)
    {
        var match = H1Regex().Match(html);
        return match.Success ? Decode(match.Groups[1].Value) : null;
    }

    private static string? FirstSeriesCandidate(string html)
    {
        var typeThenName = TvSeriesTypeThenNameRegex().Match(html);
        if (typeThenName.Success)
        {
            return Decode(typeThenName.Groups[1].Value);
        }

        var nameThenType = TvSeriesNameThenTypeRegex().Match(html);
        return nameThenType.Success ? Decode(nameThenType.Groups[1].Value) : null;
    }

    private static string CleanTitle(string title) => Decode(title);

    private static string Decode(string value) =>
        WebUtility.HtmlDecode(value).Trim();

    private static NonPodcastServiceMetaDataExtractionException MissingTitle(Uri url) =>
        new(url, "Netflix page has neither og:title nor a recoverable title/show heading.");

    /// <summary>First soft-wall <c>type</c> of Show or Movie — treated as the page primary.</summary>
    [GeneratedRegex("\"type\"\\s*:\\s*\"(Show|Movie)\"", RegexOptions.CultureInvariant)]
    private static partial Regex SoftWallPrimaryTypeRegex();

    /// <summary>First catalogue ld+json <c>@type</c> of TVSeries or Movie — treated as the page primary.</summary>
    [GeneratedRegex("\"@type\"\\s*:\\s*\"(TVSeries|Movie)\"", RegexOptions.CultureInvariant)]
    private static partial Regex CataloguePrimaryTypeRegex();

    [GeneratedRegex("\"type\"\\s*:\\s*\"Show\"", RegexOptions.CultureInvariant)]
    private static partial Regex SoftWallShowTypeRegex();

    [GeneratedRegex("<h1[^>]*>([^<]+)</h1>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex H1Regex();

    [GeneratedRegex(
        "\"@type\"\\s*:\\s*\"TVSeries\"[\\s\\S]{0,400}?\"name\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex TvSeriesTypeThenNameRegex();

    [GeneratedRegex(
        "\"name\"\\s*:\\s*\"([^\"]+)\"[\\s\\S]{0,400}?\"@type\"\\s*:\\s*\"TVSeries\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex TvSeriesNameThenTypeRegex();
}
