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

        var (duration, release) = ReadJsonLd(document);

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
            Publisher: publisher);
    }

    private static string? MetaContent(HtmlDocument document, string property)
    {
        var node = document.DocumentNode.SelectSingleNode(
            $"/html/head/meta[@property='{property}']")
                   ?? document.DocumentNode.SelectSingleNode(
                       $"//meta[@property='{property}']");
        return node?.GetAttributeValue("content", null);
    }

    private static (TimeSpan? Duration, DateTime? Release) ReadJsonLd(HtmlDocument document)
    {
        TimeSpan? duration = null;
        DateTime? release = null;
        var scripts = document.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (scripts == null)
        {
            return (null, null);
        }

        foreach (var script in scripts)
        {
            try
            {
                using var json = JsonDocument.Parse(script.InnerText);
                ReadNode(json.RootElement, ref duration, ref release);
            }
            catch (JsonException)
            {
            }
        }

        return (duration, release);
    }

    private static void ReadNode(JsonElement element, ref TimeSpan? duration, ref DateTime? release)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ReadNode(item, ref duration, ref release);
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

        if (element.TryGetProperty("@graph", out var graph))
        {
            ReadNode(graph, ref duration, ref release);
        }
    }
}
