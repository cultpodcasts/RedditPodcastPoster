using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using RedditPodcastPoster.OpenGraph.Extractors; // pragma: allowlist secret
using RedditPodcastPoster.PlayRts.Matching; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Models; // pragma: allowlist secret

namespace RedditPodcastPoster.PlayRts.Extractors; // pragma: allowlist secret

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
        var episodeTitle = FirstGroup(html, TvEpisodeNameRegex()) ??
                           FirstGroup(html, VideoObjectNameRegex());
        var title = CleanTitle(
            episodeTitle ?? openGraph?.Title ?? FirstGroup(html, DocumentTitleRegex()));
        var showName = openGraph?.ShowName ?? FirstGroup(html, PartOfSeriesNameRegex());
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
            openGraph?.Duration ?? TryParseIsoDuration(FirstGroup(html, JsonLdDurationRegex())),
            openGraph?.Release ?? TryParseTimestamp(FirstGroup(html, UploadDateRegex())),
            openGraph?.Image,
            openGraph?.Explicit,
            PlayRtsPageMetaDataExtractor.Publisher,
            showName);
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

    private static TimeSpan? TryParseIsoDuration(string? raw)
    {
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
        }

        var match = IsoDurationWithYearsMonthsRegex().Match(raw);
        if (!match.Success)
        {
            return null;
        }

        var days = ParseInt(match, 3);
        var hours = ParseInt(match, 4);
        var minutes = ParseInt(match, 5);
        var seconds = ParseInt(match, 6);
        if (days == 0 && hours == 0 && minutes == 0 && seconds == 0)
        {
            return null;
        }

        return new TimeSpan(days, hours, minutes, seconds);
    }

    private static DateTime? TryParseTimestamp(string? raw)
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

    private static int ParseInt(Match match, int group) =>
        match.Groups[group].Success
            ? int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture)
            : 0;

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

    [GeneratedRegex(
        "\"@type\"\\s*:\\s*\"TVEpisode\"[\\s\\S]{0,400}?\"name\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex TvEpisodeNameRegex();

    [GeneratedRegex(
        "\"@type\"\\s*:\\s*\"VideoObject\"[\\s\\S]{0,400}?\"name\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex VideoObjectNameRegex();

    [GeneratedRegex(
        "\"partOfSeries\"\\s*:\\s*\\{[\\s\\S]{0,200}?\"@type\"\\s*:\\s*\"TVSeries\"[\\s\\S]{0,200}?\"name\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex PartOfSeriesNameRegex();

    [GeneratedRegex("\"duration\"\\s*:\\s*\"(P[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex JsonLdDurationRegex();

    [GeneratedRegex("\"uploadDate\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex UploadDateRegex();

    [GeneratedRegex(
        "^P(?:(\\d+)Y)?(?:(\\d+)M)?(?:(\\d+)D)?(?:T(?:(\\d+)H)?(?:(\\d+)M)?(?:(\\d+)S)?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsoDurationWithYearsMonthsRegex();
}
