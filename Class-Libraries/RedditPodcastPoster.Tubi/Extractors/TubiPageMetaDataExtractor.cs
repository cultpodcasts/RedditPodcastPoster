using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Tubi.Extractors;

public interface ITubiPageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
    Task<NonPodcastServiceItemMetaData> ExtractFromHtml(Uri url, string html);
}

public class TubiPageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : ITubiPageMetaDataExtractor
{
    public const string Publisher = "Tubi";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var client = httpClientFactory.CreateClient(nameof(TubiPageMetaDataExtractor));
        using var pageResponse = await client.GetAsync(url);
        if (pageResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, pageResponse.StatusCode);
        }

        var html = await pageResponse.Content.ReadAsStringAsync();
        return await ExtractFromHtml(url, html);
    }

    public async Task<NonPodcastServiceItemMetaData> ExtractFromHtml(Uri url, string html)
    {
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

        return TubiCatalogMeta.Merge(url, html, openGraph);
    }
}

internal static partial class TubiCatalogMeta
{
    public static NonPodcastServiceItemMetaData Merge(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph)
    {
        var title = CleanTitle(
            openGraph?.Title ?? MetaContent(html, "og:title") ?? FirstGroup(html, DocumentTitleRegex()));
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "Tubi page has neither og:title nor a usable document title. Geo/login walls often return a non-catalogue shell.");
        }

        var showName = openGraph?.ShowName;
        if (IsMovie(url, html))
        {
            showName = null;
        }
        else
        {
            showName ??= FirstGroup(html, TvSeriesNameRegex());
            if (showName is null && StreamingCataloguePathHints.IsSeriesPath(url))
            {
                showName = title;
            }
        }

        if (string.Equals(showName, TubiPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase))
        {
            showName = null;
        }

        var duration = openGraph?.Duration;
        if (duration is null or { Ticks: <= 0 })
        {
            var seconds = TryVideoDurationSeconds(html);
            if (seconds is > 0)
            {
                duration = TimeSpan.FromSeconds(seconds.Value);
            }
            else
            {
                duration = TryIsoDuration(html);
            }
        }

        var release = TryMetaDate(html, "video:release_date")
                      ?? openGraph?.Release
                      ?? TryDateCreated(html);

        var description = FirstNonEmpty(
            openGraph?.Description,
            MetaContent(html, "og:description"),
            MetaContent(html, "description")) ?? string.Empty;

        var image = openGraph?.Image ?? TryAbsoluteUri(MetaContent(html, "og:image"));

        return new NonPodcastServiceItemMetaData(
            title,
            description,
            duration,
            release,
            image,
            openGraph?.Explicit,
            TubiPageMetaDataExtractor.Publisher,
            showName);
    }

    /// <summary>
    /// True when og:type is a movie, the URL is a film catalogue path, or the
    /// primary catalogue <c>@type</c> is Movie. Series paths win over later
    /// document-wide Movie blobs so recommended/carousel film JSON-LD cannot null ShowName.
    /// </summary>
    public static bool IsMovie(Uri url, string html)
    {
        var ogType = MetaContent(html, "og:type");
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
            " | Tubi",
            " - Tubi",
            " | Tubi TV"
        })
        {
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                title = title[..^suffix.Length].Trim();
            }
        }

        return title;
    }

    private static int? TryVideoDurationSeconds(string html)
    {
        var raw = MetaContent(html, "video:duration");
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) &&
               seconds > 0
            ? seconds
            : null;
    }

    private static TimeSpan? TryIsoDuration(string html)
    {
        var raw = FirstGroup(html, IsoDurationRegex());
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return XmlConvert.ToTimeSpan(raw);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static DateTime? TryMetaDate(string html, string key) => TryParseUtc(MetaContent(html, key));

    private static DateTime? TryDateCreated(string html) => TryParseUtc(FirstGroup(html, DateCreatedRegex()));

    private static DateTime? TryParseUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTime.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static Uri? TryAbsoluteUri(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            ? uri
            : null;

    private static string? MetaContent(string html, string key)
    {
        foreach (Match match in MetaTag().Matches(html))
        {
            var name = match.Groups["name"].Value;
            if (name.Length == 0)
            {
                name = match.Groups["nameAlt"].Value;
            }

            if (!name.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = match.Groups["content"].Value;
            if (value.Length == 0)
            {
                value = match.Groups["contentAlt"].Value;
            }

            return string.IsNullOrWhiteSpace(value) ? null : WebUtility.HtmlDecode(value).Trim();
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? FirstGroup(string html, Regex regex)
    {
        var match = regex.Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
    }

    [GeneratedRegex("<title>([^<]*)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentTitleRegex();

    [GeneratedRegex(
        @"<meta\b(?=[^>]*\b(?:property|name)\s*=\s*[""'](?<name>[^""']+)[""'])(?=[^>]*\bcontent\s*=\s*[""'](?<content>[^""']*)[""'])[^>]*>|<meta\b(?=[^>]*\bcontent\s*=\s*[""'](?<contentAlt>[^""']*)[""'])(?=[^>]*\b(?:property|name)\s*=\s*[""'](?<nameAlt>[^""']+)[""'])[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaTag();

    [GeneratedRegex("\"duration\"\\s*:\\s*\"(PT[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDurationRegex();

    [GeneratedRegex("\"dateCreated\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex DateCreatedRegex();

    [GeneratedRegex("\"@type\"\\s*:\\s*\"(TVSeries|Movie)\"", RegexOptions.CultureInvariant)]
    private static partial Regex CataloguePrimaryTypeRegex();

    [GeneratedRegex(
        "\"@type\"\\s*:\\s*\"TVSeries\"[\\s\\S]{0,400}?\"name\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex TvSeriesNameRegex();
}
