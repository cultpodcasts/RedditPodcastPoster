using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.HboMax.Extractors;

public interface IHboMaxPageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class HboMaxPageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : IHboMaxPageMetaDataExtractor
{
    public const string Publisher = "HBO Max";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var client = httpClientFactory.CreateClient(nameof(HboMaxPageMetaDataExtractor));
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

        return HboMaxCatalogMeta.Merge(url, html, openGraph);
    }
}

internal static partial class HboMaxCatalogMeta
{
    public static NonPodcastServiceItemMetaData Merge(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph)
    {
        var rawTitle = CleanTitle(openGraph?.Title ?? FirstGroup(html, DocumentTitleRegex()));
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "HBO Max page has neither og:title nor a usable document title. Geo/login walls often return a non-catalogue shell.");
        }

        var (duration, release) = CatalogPageTimingMeta.Coalesce(
            openGraph?.Duration,
            openGraph?.Release,
            html);

        if (IsMovie(url, html))
        {
            return new NonPodcastServiceItemMetaData(
                rawTitle,
                WbdCatalogPageMeta.CleanWatchMarketingDescription(openGraph?.Description),
                duration,
                release,
                WbdCatalogPageMeta.TryEpisodeImage(url, html, openGraph?.Image),
                openGraph?.Explicit,
                HboMaxPageMetaDataExtractor.Publisher,
                ShowName: null,
                MadeAsFilm: true);
        }

        var rawDescription = openGraph?.Description ?? string.Empty;
        var description = WbdCatalogPageMeta.CleanWatchMarketingDescription(rawDescription);
        var showName = ResolveShowName(url, html, openGraph, rawTitle);
        var title = ResolveEpisodeTitle(url, rawTitle, rawDescription, showName) ?? rawTitle;
        var image = WbdCatalogPageMeta.TryEpisodeImage(url, html, openGraph?.Image);

        return new NonPodcastServiceItemMetaData(
            title,
            description,
            duration,
            release,
            image,
            openGraph?.Explicit,
            HboMaxPageMetaDataExtractor.Publisher,
            showName,
            MadeAsFilm: false);
    }

    /// <summary>
    /// Prefer <c>og:image:alt</c>, then JSON-LD TVSeries, then a parsed
    /// <c>Watch {Show} | Season…</c> title (discovery+ parity).
    /// </summary>
    private static string? ResolveShowName(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph,
        string cleanedOgTitle)
    {
        var fromAlt = FirstGroup(html, OgImageAltRegex());
        var fromJsonLd = openGraph?.ShowName ?? FirstGroup(html, TvSeriesNameRegex());
        var fromTitle = WbdCatalogPageMeta.ParseShowNameFromWatchTitle(cleanedOgTitle);

        var showName = OpenGraphSeriesName.FromDistinctCandidates(
            cleanedOgTitle,
            HboMaxPageMetaDataExtractor.Publisher,
            fromAlt,
            fromJsonLd,
            fromTitle);

        if (showName is null &&
            StreamingCataloguePathHints.IsSeriesPath(url) &&
            !WbdCatalogPageMeta.IsEpisodePath(url))
        {
            showName = fromTitle ?? WbdCatalogPageMeta.StripWatchPrefix(cleanedOgTitle);
        }

        if (string.Equals(showName, HboMaxPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return showName;
    }

    private static string? ResolveEpisodeTitle(
        Uri url,
        string cleanedOgTitle,
        string? description,
        string? showName)
    {
        if (!WbdCatalogPageMeta.IsEpisodePath(url))
        {
            return WbdCatalogPageMeta.StripWatchPrefix(cleanedOgTitle);
        }

        var fromDescription = WbdCatalogPageMeta.TryEpisodeTitleFromDescription(description);
        if (!string.IsNullOrWhiteSpace(fromDescription))
        {
            return fromDescription;
        }

        var fromSlug = EpisodeTitleFromPath(url);
        if (!string.IsNullOrWhiteSpace(fromSlug))
        {
            return fromSlug;
        }

        var afterSeason = SeasonEpisodeTailRegex().Match(cleanedOgTitle);
        if (afterSeason.Success)
        {
            return afterSeason.Groups[1].Value.Trim();
        }

        if (!string.IsNullOrWhiteSpace(showName) &&
            cleanedOgTitle.StartsWith(showName, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = cleanedOgTitle[showName.Length..].Trim().TrimStart('|', ':', '-').Trim();
            if (!string.IsNullOrWhiteSpace(remainder))
            {
                return remainder;
            }
        }

        return WbdCatalogPageMeta.StripWatchPrefix(cleanedOgTitle);
    }

    private static string? EpisodeTitleFromPath(Uri url)
    {
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var match = EpisodeSlugRegex().Match(part);
            if (!match.Success)
            {
                continue;
            }

            var slug = match.Groups[1].Value.Replace('-', ' ').Trim();
            if (string.IsNullOrWhiteSpace(slug))
            {
                continue;
            }

            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug);
        }

        return null;
    }

    /// <summary>
    /// True when og:type is a movie, the URL is a film catalogue path, or the
    /// <em>primary</em> catalogue <c>@type</c> is Movie. Series paths win over later
    /// document-wide Movie blobs so recommended/carousel film JSON-LD cannot null ShowName.
    /// </summary>
    public static bool IsMovie(Uri url, string html)
    {
        var ogType = FirstGroup(html, OgTypeRegex());
        if (string.Equals(ogType, "video.movie", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ogType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (StreamingCataloguePathHints.IsSeriesPath(url))
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

    private static string CleanTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var title = WebUtility.HtmlDecode(raw).Trim();

        foreach (var suffix in new[]
        {
            " | Max",
            " - Max",
            " | HBO Max",
            " - HBO Max",
        })
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

    [GeneratedRegex("(?:property|name)=\"og:image:alt\"[^>]*content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgImageAltRegex();

    [GeneratedRegex("\"@type\"\\s*:\\s*\"(TVSeries|Movie)\"", RegexOptions.CultureInvariant)]
    private static partial Regex CataloguePrimaryTypeRegex();

    [GeneratedRegex(
        "\"@type\"\\s*:\\s*\"TVSeries\"[\\s\\S]{0,400}?\"name\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex TvSeriesNameRegex();

    [GeneratedRegex(
        @"\|\s*(Season\s+\d+\s+Episode\s+\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonEpisodeTailRegex();

    [GeneratedRegex(@"^e\d+-(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeSlugRegex();
}
