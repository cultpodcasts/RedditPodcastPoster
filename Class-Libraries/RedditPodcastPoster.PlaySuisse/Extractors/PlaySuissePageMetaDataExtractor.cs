using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
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
        var rawTitle = openGraph?.Title ?? FirstGroup(html, DocumentTitleRegex());
        var title = StripSeasonHubSuffix(CleanTitle(rawTitle));
        var (rootEpisodeArray, firstEpisodeName) = ReadRootTvSeriesHub(html);
        var seriesHub = IsSeriesHub(url, rawTitle, rootEpisodeArray);

        if (seriesHub && !string.IsNullOrWhiteSpace(firstEpisodeName))
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

        var duration = openGraph?.Duration;
        if (seriesHub)
        {
            duration ??= TryParseSeconds(FirstGroup(decodedHtml, FirstEpisodeDurationRegex()));
        }

        return new NonPodcastServiceItemMetaData(
            title,
            openGraph?.Description ?? string.Empty,
            duration,
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

    /// <summary>
    /// Season-hub rewrite (first-episode title, firstEpisodeDuration, year-only release drop)
    /// is gated on catalogue URL shape <c>/detail</c> or <c>/show</c> — never <c>/watch</c> —
    /// plus a season-suffix og:title and/or a <em>root</em> <c>TVSeries.episode</c> array.
    /// Watch pages that embed series JSON-LD must keep their own og:title.
    /// </summary>
    private static bool IsSeriesHub(Uri url, string? rawTitle, bool rootEpisodeArray)
    {
        if (!IsSeasonHubCataloguePath(url))
        {
            return false;
        }

        return rootEpisodeArray || HasSeasonHubSuffix(rawTitle);
    }

    private static bool IsSeasonHubCataloguePath(Uri url)
    {
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var localeOffset = parts.Length > 0 && parts[0].Length == 2 ? 1 : 0;
        if (parts.Length <= localeOffset)
        {
            return false;
        }

        var kind = parts[localeOffset];
        return kind.Equals("detail", StringComparison.OrdinalIgnoreCase) ||
               kind.Equals("show", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSeasonHubSuffix(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return false;
        }

        return SeasonHubSuffixRegex().IsMatch(CleanTitle(rawTitle));
    }

    private static (bool HasEpisodeArray, string? FirstEpisodeName) ReadRootTvSeriesHub(string html)
    {
        var hasEpisodeArray = false;
        string? firstEpisodeName = null;
        foreach (Match script in JsonLdScriptRegex().Matches(html))
        {
            try
            {
                using var json = JsonDocument.Parse(WebUtility.HtmlDecode(script.Groups[1].Value));
                WalkRootTvSeries(json.RootElement, ref hasEpisodeArray, ref firstEpisodeName);
            }
            catch (JsonException)
            {
            }
        }

        return (hasEpisodeArray, firstEpisodeName);
    }

    private static void WalkRootTvSeries(
        JsonElement element,
        ref bool hasEpisodeArray,
        ref string? firstEpisodeName)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                WalkRootTvSeries(item, ref hasEpisodeArray, ref firstEpisodeName);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("@graph", out var graph))
        {
            WalkRootTvSeries(graph, ref hasEpisodeArray, ref firstEpisodeName);
            return;
        }

        if (!IsJsonLdType(element, "TVSeries") ||
            !element.TryGetProperty("episode", out var hubItems) ||
            hubItems.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        hasEpisodeArray = true;
        foreach (var episode in hubItems.EnumerateArray())
        {
            if (episode.ValueKind != JsonValueKind.Object ||
                !IsJsonLdType(episode, "TVEpisode") ||
                !IsEpisodeNumberOne(episode))
            {
                continue;
            }

            if (episode.TryGetProperty("name", out var name) &&
                name.ValueKind == JsonValueKind.String)
            {
                firstEpisodeName ??= name.GetString();
                return;
            }
        }
    }

    private static bool IsJsonLdType(JsonElement element, string expectedType)
    {
        if (!element.TryGetProperty("@type", out var typeElement))
        {
            return false;
        }

        if (typeElement.ValueKind == JsonValueKind.String)
        {
            return string.Equals(typeElement.GetString(), expectedType, StringComparison.OrdinalIgnoreCase);
        }

        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in typeElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String &&
                    string.Equals(item.GetString(), expectedType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsEpisodeNumberOne(JsonElement episode)
    {
        if (!episode.TryGetProperty("episodeNumber", out var number))
        {
            return false;
        }

        if (number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var asInt))
        {
            return asInt == 1;
        }

        return number.ValueKind == JsonValueKind.String &&
               int.TryParse(number.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
               parsed == 1;
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

        return title;
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
        "<script[^>]*type\\s*=\\s*[\"']application/ld\\+json[\"'][^>]*>([\\s\\S]*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonLdScriptRegex();

    [GeneratedRegex("\"firstEpisodeDuration\"\\s*:\\s*\"?(\\d+)\"?", RegexOptions.CultureInvariant)]
    private static partial Regex FirstEpisodeDurationRegex();

    [GeneratedRegex("\"image\"\\s*:\\s*\"(https://playsuisse-img[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex JsonLdImageRegex();

    [GeneratedRegex(
        "\\s*-\\s*(?:Saison|Staffel|Stagione|Season)\\s+\\d+\\s*-\\s*(?:Série|Serie|Series)\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonHubSuffixRegex();
}
