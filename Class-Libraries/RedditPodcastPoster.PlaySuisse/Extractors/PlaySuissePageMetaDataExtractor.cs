using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PlaySuisse.Extractors;

public interface IPlaySuissePageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class PlaySuissePageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : IPlaySuissePageMetaDataExtractor
{
    public const string Publisher = "Play Suisse";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var client = httpClientFactory.CreateClient(nameof(PlaySuissePageMetaDataExtractor));
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

        return PlaySuisseCatalogMeta.Merge(url, html, openGraph);
    }
}

internal static partial class PlaySuisseCatalogMeta
{
    public static NonPodcastServiceItemMetaData Merge(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph)
    {
        var decodedHtml = html.Replace("\\\"", "\"", StringComparison.Ordinal);
        var seriesName = openGraph?.ShowName ?? FirstGroup(decodedHtml, TvSeriesNameRegex());
        var firstEpisodeName = FirstGroup(decodedHtml, FirstEpisodeNameRegex()) ??
                               FirstGroup(decodedHtml, FirstEpisodeNameNumberFirstRegex());
        var title = CleanTitle(openGraph?.Title ?? FirstGroup(html, DocumentTitleRegex()));
        title = StripSeasonHubSuffix(title);
        var seriesHub = IsSeriesHub(decodedHtml, title, seriesName);

        if (!string.IsNullOrWhiteSpace(firstEpisodeName) && seriesHub)
        {
            title = firstEpisodeName;
        }
        else if (string.IsNullOrWhiteSpace(title))
        {
            title = seriesName ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "Play Suisse page has neither og:title nor a usable document title. Geo/login walls often return a non-catalogue shell.");
        }

        var showName = seriesName;
        if (IsMovie(url, html))
        {
            showName = null;
        }
        else
        {
            if (showName is null && StreamingCataloguePathHints.IsSeriesPath(url))
            {
                showName = title;
            }
        }

        if (string.Equals(showName, PlaySuissePageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase))
        {
            showName = null;
        }

        return new NonPodcastServiceItemMetaData(
            title,
            openGraph?.Description ?? string.Empty,
            openGraph?.Duration ?? TryParseSeconds(FirstGroup(decodedHtml, FirstEpisodeDurationRegex())),
            DropYearOnlyHubRelease(openGraph?.Release, seriesHub),
            PreferCatalogueImage(decodedHtml, openGraph?.Image),
            openGraph?.Explicit,
            PlaySuissePageMetaDataExtractor.Publisher,
            showName);
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

    private static bool IsSeriesHub(string html, string title, string? seriesName)
    {
        if (FirstGroup(html, FirstEpisodeDurationRegex()) is not null ||
            FirstGroup(html, FirstEpisodeNameRegex()) is not null ||
            FirstGroup(html, FirstEpisodeNameNumberFirstRegex()) is not null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(seriesName))
        {
            return false;
        }

        return title.Equals(seriesName, StringComparison.OrdinalIgnoreCase) ||
               title.StartsWith(seriesName, StringComparison.OrdinalIgnoreCase);
    }

    private static Uri? PreferCatalogueImage(string html, Uri? openGraphImage)
    {
        var jsonLdImage = ExpandImageTemplate(FirstGroup(html, JsonLdImageRegex()));
        if (jsonLdImage is not null)
        {
            return jsonLdImage;
        }

        return ExpandImageTemplate(openGraphImage?.ToString()) ?? openGraphImage;
    }

    private static Uri? ExpandImageTemplate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var resolved = WebUtility.HtmlDecode(raw)
            .Replace("{width}", "1920", StringComparison.Ordinal);
        return Uri.TryCreate(resolved, UriKind.Absolute, out var url) ? url : null;
    }

    private static DateTime? DropYearOnlyHubRelease(DateTime? release, bool seriesHub)
    {
        if (!seriesHub || release is null)
        {
            return release;
        }

        if (release is { Month: 1, Day: 1 } && release.Value.TimeOfDay == TimeSpan.Zero)
        {
            return null;
        }

        return release;
    }

    private static TimeSpan? TryParseSeconds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) &&
               seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : null;
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
            " | Play Suisse",
            " - Play Suisse",
        })
        {
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                title = title[..^suffix.Length].Trim();
            }
        }

        return StripSeasonHubSuffix(title);
    }

    private static string StripSeasonHubSuffix(string title)
    {
        var stripped = SeasonHubSuffixRegex().Replace(title, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(stripped) ? title : stripped;
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

    [GeneratedRegex(
        "\"@type\"\\s*:\\s*\"TVEpisode\"[\\s\\S]{0,400}?\"name\"\\s*:\\s*\"([^\"]+)\"[\\s\\S]{0,200}?\"episodeNumber\"\\s*:\\s*1\\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex FirstEpisodeNameRegex();

    [GeneratedRegex(
        "\"@type\"\\s*:\\s*\"TVEpisode\"[\\s\\S]{0,400}?\"episodeNumber\"\\s*:\\s*1\\b[\\s\\S]{0,200}?\"name\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex FirstEpisodeNameNumberFirstRegex();

    [GeneratedRegex("\"firstEpisodeDuration\"\\s*:\\s*\"?(\\d+)\"?", RegexOptions.CultureInvariant)]
    private static partial Regex FirstEpisodeDurationRegex();

    [GeneratedRegex("\"image\"\\s*:\\s*\"(https://playsuisse-img[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex JsonLdImageRegex();

    [GeneratedRegex(
        "\\s*-\\s*(?:Saison|Staffel|Stagione|Season)\\s+\\d+\\s*-\\s*(?:Série|Serie|Series)\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonHubSuffixRegex();
}
