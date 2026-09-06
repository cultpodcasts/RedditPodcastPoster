using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.OpenGraph.Extractors;

public class OpenGraphPageMetaDataExtractor
{
    private static readonly Regex IsoDurationWithYearsMonths = new(
        @"^P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public async Task<NonPodcastServiceItemMetaData> Extract(
        Uri url,
        HttpResponseMessage pageResponse,
        string publisher)
    {
        var document = new HtmlDocument();
        document.Load(await pageResponse.Content.ReadAsStreamAsync());
        var title = MetaContent(document, "og:title");
        var description = MetaContent(document, "og:description") ?? string.Empty;
        var imageValue = MetaContent(document, "og:image");
        Uri? image = null;
        if (!string.IsNullOrWhiteSpace(imageValue) &&
            Uri.TryCreate(imageValue, UriKind.Absolute, out var imageUrl))
        {
            image = imageUrl;
        }

        var (duration, release, jsonLdSeries, jsonLdName) = ReadJsonLd(document);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = jsonLdName;
        }

        var showName = OpenGraphSeriesName.FromDistinctCandidates(
            title,
            publisher,
            MetaContent(document, "og:video:series"),
            MetaContent(document, "og:series"),
            jsonLdSeries);

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "Page does not have an og:title meta tag.");
        }

        return new NonPodcastServiceItemMetaData(
            title,
            description,
            duration,
            release,
            image,
            Publisher: publisher,
            ShowName: showName,
            JsonLdName: jsonLdName);
    }

    private static string? MetaContent(HtmlDocument document, string property)
    {
        var node = document.DocumentNode.SelectSingleNode(
            $"/html/head/meta[@property='{property}']")
                   ?? document.DocumentNode.SelectSingleNode(
                       $"//meta[@property='{property}']")
                   ?? document.DocumentNode.SelectSingleNode(
                       $"//meta[@name='{property}']");
        var content = node?.GetAttributeValue("content", null);
        return content is null ? null : WebUtility.HtmlDecode(content);
    }

    private static (TimeSpan? Duration, DateTime? Release, string? Series, string? Name) ReadJsonLd(HtmlDocument document)
    {
        TimeSpan? duration = null;
        DateTime? release = null;
        string? series = null;
        string? name = null;
        var scripts = document.DocumentNode.SelectNodes(
            "//script[@type='application/ld+json'] | //script[contains(@type,'ld+json')]");
        if (scripts == null)
        {
            return (null, null, null, null);
        }

        foreach (var script in scripts)
        {
            try
            {
                var jsonText = System.Net.WebUtility.HtmlDecode(script.InnerText);
                using var json = JsonDocument.Parse(jsonText);
                ReadNode(json.RootElement, ref duration, ref release, ref series, ref name);
            }
            catch (JsonException)
            {
            }
        }

        return (duration, release, series, name);
    }

    private static void ReadNode(
        JsonElement element,
        ref TimeSpan? duration,
        ref DateTime? release,
        ref string? series,
        ref string? name)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ReadNode(item, ref duration, ref release, ref series, ref name);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (name is null &&
            (IsJsonLdType(element, "TVEpisode") || IsJsonLdType(element, "VideoObject")) &&
            element.TryGetProperty("name", out var jsonLdName) &&
            jsonLdName.ValueKind == JsonValueKind.String)
        {
            name = jsonLdName.GetString();
        }

        if (element.TryGetProperty("duration", out var durationElement) &&
            durationElement.ValueKind == JsonValueKind.String &&
            duration is null)
        {
            duration = TryParseIsoDuration(durationElement.GetString());
        }

        if (element.TryGetProperty("datePublished", out var published) &&
            published.ValueKind == JsonValueKind.String &&
            release is null &&
            DateTime.TryParse(
                published.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedPublished))
        {
            release = parsedPublished;
        }

        if (element.TryGetProperty("uploadDate", out var uploaded) &&
            uploaded.ValueKind == JsonValueKind.String &&
            release is null &&
            DateTime.TryParse(
                uploaded.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedUpload))
        {
            release = parsedUpload;
        }

        if (series is null &&
            element.TryGetProperty("partOfSeries", out var partOfSeries) &&
            partOfSeries.ValueKind == JsonValueKind.Object &&
            partOfSeries.TryGetProperty("name", out var seriesName) &&
            seriesName.ValueKind == JsonValueKind.String)
        {
            series = seriesName.GetString();
        }

        // Catalogue pages expose @type TVSeries with name (not partOfSeries). Movies must not become ShowName.
        if (series is null &&
            IsJsonLdType(element, "TVSeries") &&
            element.TryGetProperty("name", out var tvSeriesName) &&
            tvSeriesName.ValueKind == JsonValueKind.String)
        {
            series = tvSeriesName.GetString();
        }

        if (element.TryGetProperty("@graph", out var graph))
        {
            ReadNode(graph, ref duration, ref release, ref series, ref name);
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

        var match = IsoDurationWithYearsMonths.Match(raw);
        if (!match.Success)
        {
            return null;
        }

        var days = ParseDurationInt(match, 3);
        var hours = ParseDurationInt(match, 4);
        var minutes = ParseDurationInt(match, 5);
        var seconds = ParseDurationInt(match, 6);
        if (days == 0 && hours == 0 && minutes == 0 && seconds == 0)
        {
            return null;
        }

        return new TimeSpan(days, hours, minutes, seconds);
    }

    private static int ParseDurationInt(Match match, int group) =>
        match.Groups[group].Success
            ? int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture)
            : 0;
}
