using System.Globalization;
using System.Text.Json;
using System.Xml;
using HtmlAgilityPack;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.OpenGraph.Extractors;

public class OpenGraphPageMetaDataExtractor
{
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

        var (duration, release, jsonLdSeries) = ReadJsonLd(document);
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
            ShowName: showName);
    }

    private static string? MetaContent(HtmlDocument document, string property)
    {
        var node = document.DocumentNode.SelectSingleNode(
            $"/html/head/meta[@property='{property}']")
                   ?? document.DocumentNode.SelectSingleNode(
                       $"//meta[@property='{property}']");
        return node?.GetAttributeValue("content", null);
    }

    private static (TimeSpan? Duration, DateTime? Release, string? Series) ReadJsonLd(HtmlDocument document)
    {
        TimeSpan? duration = null;
        DateTime? release = null;
        string? series = null;
        var scripts = document.DocumentNode.SelectNodes(
            "//script[@type='application/ld+json'] | //script[contains(@type,'ld+json')]");
        if (scripts == null)
        {
            return (null, null, null);
        }

        foreach (var script in scripts)
        {
            try
            {
                var jsonText = System.Net.WebUtility.HtmlDecode(script.InnerText);
                using var json = JsonDocument.Parse(jsonText);
                ReadNode(json.RootElement, ref duration, ref release, ref series);
            }
            catch (JsonException)
            {
            }
        }

        return (duration, release, series);
    }

    private static void ReadNode(
        JsonElement element,
        ref TimeSpan? duration,
        ref DateTime? release,
        ref string? series)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ReadNode(item, ref duration, ref release, ref series);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("duration", out var durationElement) &&
            durationElement.ValueKind == JsonValueKind.String &&
            duration is null)
        {
            try
            {
                duration = XmlConvert.ToTimeSpan(durationElement.GetString()!);
            }
            catch (FormatException)
            {
            }
        }

        if (element.TryGetProperty("datePublished", out var published) &&
            published.ValueKind == JsonValueKind.String &&
            release is null &&
            DateTime.TryParse(
                published.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            release = parsed;
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
            ReadNode(graph, ref duration, ref release, ref series);
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
}
