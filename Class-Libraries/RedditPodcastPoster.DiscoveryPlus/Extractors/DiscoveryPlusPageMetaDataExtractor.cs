using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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

        var (duration, release) = ResolveTiming(openGraph, html);

        if (IsMovie(url, html))
        {
            return new NonPodcastServiceItemMetaData(
                rawTitle,
                WbdCatalogPageMeta.CleanWatchMarketingDescription(openGraph?.Description),
                duration,
                release,
                WbdCatalogPageMeta.TryEpisodeImage(url, html, openGraph?.Image),
                openGraph?.Explicit,
                DiscoveryPlusPageMetaDataExtractor.Publisher,
                ShowName: null,
                MadeAsFilm: true);
        }

        var rawDescription = openGraph?.Description ?? string.Empty;
        var description = WbdCatalogPageMeta.ResolveEpisodeDescription(url, html, rawDescription);
        var showName = WbdCatalogPageMeta.ResolveShowName(
            url,
            html,
            openGraph,
            rawTitle,
            DiscoveryPlusPageMetaDataExtractor.Publisher);
        var title = WbdCatalogPageMeta.ResolveEpisodeTitle(url, rawTitle, rawDescription, showName) ?? rawTitle;
        var image = WbdCatalogPageMeta.TryEpisodeImage(url, html, openGraph?.Image);

        return new NonPodcastServiceItemMetaData(
            title,
            description,
            duration,
            release,
            image,
            openGraph?.Explicit,
            DiscoveryPlusPageMetaDataExtractor.Publisher,
            showName,
            MadeAsFilm: false);
    }

    /// <summary>
    /// Open Graph / JSON-LD first (<see cref="CatalogPageTimingMeta"/>), then discovery+ geo
    /// availability windows when release is still missing or year-only (SSR omits episode air dates).
    /// Duration is often absent from discovery+ HTML shells — leave null rather than inventing.
    /// </summary>
    private static (TimeSpan? Duration, DateTime? Release) ResolveTiming(
        NonPodcastServiceItemMetaData? openGraph,
        string html)
    {
        var (duration, release) = CatalogPageTimingMeta.Coalesce(
            openGraph?.Duration,
            openGraph?.Release,
            html);

        var availability = TryEarliestAvailabilityWindowStart(html);
        if (availability is null)
        {
            return (duration, release);
        }

        if (release is null || IsYearOnlyUtcMidnight(release.Value))
        {
            // Availability is a datetime; catalogue air dates are calendar dates (UTC midnight).
            release = DateTime.SpecifyKind(availability.Value.Date, DateTimeKind.Utc);
        }

        return (duration, release);
    }

    private static bool IsYearOnlyUtcMidnight(DateTime release) =>
        release.Kind == DateTimeKind.Utc &&
        release is { Month: 1, Day: 1, TimeOfDay: { Ticks: 0 } };

    /// <summary>
    /// discovery+ geo-redirect embeds <c>pageLocales.windows</c> like
    /// <c>"us/en":["2026-11-26T02:00:00Z","2099-…"]</c> — the earliest start is the best SSR air hint.
    /// </summary>
    private static DateTime? TryEarliestAvailabilityWindowStart(string html)
    {
        DateTime? earliest = null;
        foreach (Match windows in AvailabilityWindowsObjectRegex().Matches(html))
        {
            foreach (Match startMatch in AvailabilityWindowStartRegex().Matches(windows.Groups[1].Value))
            {
                if (!DateTime.TryParse(
                        startMatch.Groups[1].Value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var start))
                {
                    continue;
                }

                if (earliest is null || start < earliest)
                {
                    earliest = start;
                }
            }
        }

        return earliest;
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

    [GeneratedRegex("\"@type\"\\s*:\\s*\"(TVSeries|Movie)\"", RegexOptions.CultureInvariant)]
    private static partial Regex CataloguePrimaryTypeRegex();

    [GeneratedRegex(
        @"""windows""\s*:\s*\{([^}]*)\}",
        RegexOptions.CultureInvariant)]
    private static partial Regex AvailabilityWindowsObjectRegex();

    [GeneratedRegex(
        @"""[^""]+""\s*:\s*\[\s*""(\d{4}-\d{2}-\d{2}T[^""]+)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex AvailabilityWindowStartRegex();
}
