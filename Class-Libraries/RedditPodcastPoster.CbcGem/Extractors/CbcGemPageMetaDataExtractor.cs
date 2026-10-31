using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;
using RedditPodcastPoster.CbcGem.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.CbcGem.Extractors;

public interface ICbcGemPageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);

    Task<NonPodcastServiceItemMetaData> ExtractFromHtml(Uri url, string html);
}

/// <summary>
/// CBC Gem pages are server-rendered with one schema.org JSON-LD block. <c>og:type</c> is
/// <c>video.tv_show</c> on every page (films included) and <c>og:url</c> on an episode points at
/// the series hub, so classification and metadata come from JSON-LD, not Open Graph.
/// </summary>
public class CbcGemPageMetaDataExtractor(IHttpClientFactory httpClientFactory) : ICbcGemPageMetaDataExtractor
{
    public const string Publisher = "CBC Gem";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var canonical = CbcGemUrlMatcher.CanonicalUrl(url);
        var client = httpClientFactory.CreateClient(nameof(CbcGemPageMetaDataExtractor));
        using var pageResponse = await client.GetAsync(canonical);
        if (pageResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new NonPodcastServiceMetaDataExtractionException(canonical, pageResponse.StatusCode);
        }

        var html = await pageResponse.Content.ReadAsStringAsync();
        return CbcGemCatalogMeta.Parse(canonical, html);
    }

    public Task<NonPodcastServiceItemMetaData> ExtractFromHtml(Uri url, string html) =>
        Task.FromResult(CbcGemCatalogMeta.Parse(CbcGemUrlMatcher.CanonicalUrl(url), html));
}

internal static partial class CbcGemCatalogMeta
{
    private const string ImageSizeToken = "(_Size_)";
    private const string ImageSize = "1200";
    private const string GenericSocialImageMarker = "cbc-gem-social-media";

    public static NonPodcastServiceItemMetaData Parse(Uri url, string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var item = FindCatalogueItem(document);
        if (item is null)
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "CBC Gem page has no Movie or TVEpisode JSON-LD, so it is not a catalogue film or episode.");
        }

        using var owner = item.Value.Document;
        var node = item.Value.Element;
        var type = item.Value.Type;

        if (type == "TVSeries")
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "CBC Gem series page. Submit a film page or an episode page (/{show}/s{NN}e{NN}).");
        }

        var title = Decode(StringProperty(node, "name")) ?? TitleFromOpenGraph(document);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "CBC Gem page has no title in JSON-LD or og:title.");
        }

        string? showName = null;
        if (type == "TVEpisode")
        {
            showName = Decode(SeriesName(node)) ?? SeriesFromOpenGraph(document);
            if (string.IsNullOrWhiteSpace(showName) ||
                string.Equals(showName, CbcGemPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase))
            {
                throw new NonPodcastServiceMetaDataExtractionException(
                    url,
                    "CBC Gem episode page has no series name, so it cannot be filed under a show.");
            }
        }

        var description = Decode(StringProperty(node, "abstract"))
                          ?? Decode(StringProperty(node, "description"))
                          ?? MetaContent(document, "og:description")
                          ?? MetaContent(document, "description")
                          ?? string.Empty;

        return new NonPodcastServiceItemMetaData(
            title,
            description,
            ParseDuration(StringProperty(node, "duration")),
            ParseRelease(StringProperty(node, "datePublished")),
            ResolveImage(node, document),
            Explicit: null,
            Publisher: CbcGemPageMetaDataExtractor.Publisher,
            ShowName: showName,
            JsonLdName: title,
            MadeAsFilm: type == "Movie");
    }

    /// <summary>
    /// A time-stamped value converts to UTC. A date-only value stays at midnight, unshifted.
    /// </summary>
    internal static DateTime? ParseRelease(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (DateOnlyRegex().IsMatch(trimmed) &&
            DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var date))
        {
            return date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        }

        return DateTime.TryParse(
            trimmed,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    internal static TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var duration = XmlConvert.ToTimeSpan(value.Trim());
            return duration > TimeSpan.Zero ? duration : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static (JsonDocument Document, JsonElement Element, string Type)? FindCatalogueItem(HtmlDocument document)
    {
        var scripts = document.DocumentNode.SelectNodes("//script[contains(@type,'ld+json')]");
        if (scripts is null)
        {
            return null;
        }

        foreach (var script in scripts)
        {
            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(script.InnerText);
            }
            catch (JsonException)
            {
                continue;
            }

            if (TryFind(json.RootElement, out var element, out var type))
            {
                return (json, element, type);
            }

            json.Dispose();
        }

        return null;
    }

    private static bool TryFind(JsonElement element, out JsonElement found, out string type)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (TryFind(child, out found, out type))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in new[] { "Movie", "TVEpisode", "TVSeries" })
            {
                if (HasType(element, candidate))
                {
                    found = element;
                    type = candidate;
                    return true;
                }
            }

            if (element.TryGetProperty("@graph", out var graph) && TryFind(graph, out found, out type))
            {
                return true;
            }
        }

        found = default;
        type = string.Empty;
        return false;
    }

    private static bool HasType(JsonElement element, string expected)
    {
        if (!element.TryGetProperty("@type", out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => string.Equals(value.GetString(), expected, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => value.EnumerateArray().Any(entry =>
                entry.ValueKind == JsonValueKind.String &&
                string.Equals(entry.GetString(), expected, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    /// <summary>
    /// CBC Gem spells the key <c>partofSeries</c>; schema.org spells it <c>partOfSeries</c>. Accept both.
    /// </summary>
    private static string? SeriesName(JsonElement node)
    {
        var series = PropertyIgnoreCase(node, "partOfSeries");
        return series is { ValueKind: JsonValueKind.Object } value ? StringProperty(value, "name") : null;
    }

    private static Uri? ResolveImage(JsonElement node, HtmlDocument document)
    {
        var candidates = new List<string?> { ImageValue(PropertyIgnoreCase(node, "image")) };
        foreach (var parent in new[] { "partOfSeason", "partOfSeries" })
        {
            if (PropertyIgnoreCase(node, parent) is { ValueKind: JsonValueKind.Object } value)
            {
                candidates.Add(ImageValue(PropertyIgnoreCase(value, "image")));
            }
        }

        var ogImage = MetaContent(document, "og:image");
        if (ogImage is not null && !ogImage.Contains(GenericSocialImageMarker, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(ogImage);
        }

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var sized = candidate.Replace(ImageSizeToken, ImageSize, StringComparison.Ordinal);
            if (Uri.TryCreate(sized, UriKind.Absolute, out var image) &&
                (image.Scheme == Uri.UriSchemeHttps || image.Scheme == Uri.UriSchemeHttp))
            {
                return image;
            }
        }

        return null;
    }

    private static string? ImageValue(JsonElement? image) =>
        image switch
        {
            { ValueKind: JsonValueKind.String } value => value.GetString(),
            { ValueKind: JsonValueKind.Object } value => StringProperty(value, "url") ?? StringProperty(value, "contentUrl"),
            { ValueKind: JsonValueKind.Array } value => value.EnumerateArray()
                .Select(entry => ImageValue(entry))
                .FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry)),
            _ => null
        };

    private static JsonElement? PropertyIgnoreCase(JsonElement node, string name)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in node.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? StringProperty(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object &&
        node.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static string? Decode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WebUtility.HtmlDecode(value).Trim();

    /// <summary>
    /// <c>og:title</c> is <c>{title} | {Movies|Shows|series} | CBC Gem</c>.
    /// </summary>
    private static string[] OpenGraphTitleParts(HtmlDocument document) =>
        (MetaContent(document, "og:title") ?? string.Empty)
        .Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? TitleFromOpenGraph(HtmlDocument document) =>
        OpenGraphTitleParts(document).FirstOrDefault(part =>
            !string.Equals(part, CbcGemPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase));

    private static string? SeriesFromOpenGraph(HtmlDocument document)
    {
        var parts = OpenGraphTitleParts(document);
        return parts.Length == 3 &&
               string.Equals(parts[2], CbcGemPageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : null;
    }

    private static string? MetaContent(HtmlDocument document, string key)
    {
        var node = document.DocumentNode.SelectSingleNode($"//meta[@property='{key}']")
                   ?? document.DocumentNode.SelectSingleNode($"//meta[@name='{key}']");
        var content = node?.GetAttributeValue("content", string.Empty);
        return string.IsNullOrWhiteSpace(content) ? null : WebUtility.HtmlDecode(content).Trim();
    }

    [GeneratedRegex("^\\d{4}-\\d{2}-\\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex DateOnlyRegex();
}
