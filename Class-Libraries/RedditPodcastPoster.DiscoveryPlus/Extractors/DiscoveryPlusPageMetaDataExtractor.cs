using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RedditPodcastPoster.DiscoveryPlus.Matching;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.DiscoveryPlus.Extractors;

public interface IDiscoveryPlusPageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class DiscoveryPlusPageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : IDiscoveryPlusPageMetaDataExtractor
{
    public const string Publisher = "discovery+";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var client = httpClientFactory.CreateClient(nameof(DiscoveryPlusPageMetaDataExtractor));
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

        return DiscoveryPlusCatalogMeta.Merge(url, html, openGraph);
    }
}

internal static partial class DiscoveryPlusCatalogMeta
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
                "discovery+ page has neither og:title nor a usable document title. Geo/login walls often return a non-catalogue shell.");
        }

        if (IsMovie(url, html))
        {
            return new NonPodcastServiceItemMetaData(
                rawTitle,
                openGraph?.Description ?? string.Empty,
                openGraph?.Duration,
                openGraph?.Release,
                openGraph?.Image,
                openGraph?.Explicit,
                DiscoveryPlusPageMetaDataExtractor.Publisher,
                ShowName: null,
                MadeAsFilm: true);
        }

        var description = openGraph?.Description ?? string.Empty;
        var showName = ResolveShowName(url, html, openGraph, rawTitle);
        var title = ResolveEpisodeTitle(url, rawTitle, description, showName) ?? rawTitle;

        return new NonPodcastServiceItemMetaData(
            title,
            description,
            openGraph?.Duration,
            openGraph?.Release,
            openGraph?.Image,
            openGraph?.Explicit,
            DiscoveryPlusPageMetaDataExtractor.Publisher,
            showName,
            MadeAsFilm: false);
    }

    /// <summary>
    /// Prefer <c>og:image:alt</c> (clean brand), then JSON-LD TVSeries, then a parsed
    /// <c>Watch {Show} | Season…</c> title. Never keep the full episode OG title as ShowName —
    /// that created one TvShow per episode in prod (#1587).
    /// </summary>
    private static string? ResolveShowName(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph,
        string cleanedOgTitle)
    {
        var fromAlt = FirstGroup(html, OgImageAltRegex());
        var fromJsonLd = openGraph?.ShowName ?? FirstGroup(html, TvSeriesNameRegex());
        var fromTitle = ParseShowNameFromWatchTitle(cleanedOgTitle);

        var showName = OpenGraphSeriesName.FromDistinctCandidates(
            cleanedOgTitle,
            DiscoveryPlusPageMetaDataExtractor.Publisher,
            fromAlt,
            fromJsonLd,
            fromTitle);

        if (showName is null &&
            StreamingCataloguePathHints.IsSeriesPath(url) &&
            !DiscoveryPlusUrlMatcher.IsEpisodePath(url))
        {
            // Show hub only: title-as-ShowName after stripping Watch / season tails.
            showName = fromTitle ?? StripWatchPrefix(cleanedOgTitle);
        }

        if (string.Equals(showName, DiscoveryPlusPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return showName;
    }

    private static string? ResolveEpisodeTitle(
        Uri url,
        string cleanedOgTitle,
        string description,
        string? showName)
    {
        if (!DiscoveryPlusUrlMatcher.IsEpisodePath(url))
        {
            return StripWatchPrefix(cleanedOgTitle);
        }

        var fromDescription = FirstGroup(description, DescriptionEpisodeTitleRegex());
        if (!string.IsNullOrWhiteSpace(fromDescription))
        {
            return fromDescription.Trim().TrimEnd('.');
        }

        var fromSlug = EpisodeTitleFromPath(url);
        if (!string.IsNullOrWhiteSpace(fromSlug))
        {
            return fromSlug;
        }

        // Last resort: strip "Watch {show} | Season N Episode M" down to a season label, not the show brand.
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

        return StripWatchPrefix(cleanedOgTitle);
    }

    private static string? ParseShowNameFromWatchTitle(string cleanedOgTitle)
    {
        var withoutWatch = StripWatchPrefix(cleanedOgTitle);
        var beforeSeason = SeasonSplitRegex().Match(withoutWatch);
        if (beforeSeason.Success)
        {
            return beforeSeason.Groups[1].Value.Trim().TrimEnd(':', '|', '-').Trim();
        }

        return string.IsNullOrWhiteSpace(withoutWatch) ? null : withoutWatch;
    }

    private static string StripWatchPrefix(string title)
    {
        const string watch = "Watch ";
        if (title.StartsWith(watch, StringComparison.OrdinalIgnoreCase))
        {
            return title[watch.Length..].Trim();
        }

        return title.Trim();
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
            " | discovery+",
            " - discovery+",
            " | Discovery+",
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
        @"^(.*?)\s*\|\s*Season\s+\d+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonSplitRegex();

    [GeneratedRegex(
        @"\|\s*(Season\s+\d+\s+Episode\s+\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonEpisodeTailRegex();

    /// <summary>
    /// Capture after <c>Season N, Episode M - </c> until a structural end: space-dot blurb
    /// separator, pipe, or end of string — not the first <c>.</c> (titles may contain periods).
    /// </summary>
    [GeneratedRegex(
        @"Season\s+\d+\s*,\s*Episode\s+\d+\s*-\s*(.+?)(?:\s+\.|\||$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DescriptionEpisodeTitleRegex();

    [GeneratedRegex(@"^e\d+-(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeSlugRegex();
}
