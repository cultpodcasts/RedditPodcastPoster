using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.BcVideo.Extractors;

public interface IBcVideoMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url, string html);
}

public partial class BcVideoMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    ILogger<BcVideoMetaDataExtractor> logger) : IBcVideoMetaDataExtractor
{
    private static readonly Uri VideoApi = new("https://api.bitchute.com/api/beta/video");
    private static readonly TimeSpan VideoApiRequestTimeout = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions ExtractJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var videoId = RequireVideoId(url);
        var client = httpClientFactory.CreateClient(nameof(BcVideoMetaDataExtractor));
        var fromApi = await TryVideoApiAsync(client, url, videoId);
        if (fromApi != null)
        {
            return fromApi;
        }

        var canonical = ServiceCatalog.CanonicalUrlOrSelf(ServiceKeys.BcVideo, url);
        var oEmbedTask = TryOEmbedAsync(client, canonical);
        var htmlTask = TryWatchHtmlAsync(client, canonical);
        await Task.WhenAll(oEmbedTask, htmlTask);
        var merged = Merge(await oEmbedTask, await htmlTask);
        if (merged == null || string.IsNullOrWhiteSpace(merged.Title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "BcVideo extract could not obtain a title.");
        }

        logger.LogInformation(
            "BcVideo extract fell back to oEmbed/HTML with title; duration-missing {DurationMissing} release-missing {ReleaseMissing}.",
            merged.Duration is null,
            merged.Release is null);
        return merged;
    }

    public Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url, string html)
    {
        RequireVideoId(url);
        var fromJson = TryParseExtractJson(html);
        if (fromJson != null)
        {
            return Task.FromResult(fromJson);
        }

        var fromHtml = ParseWatchHtml(html);
        if (fromHtml == null || string.IsNullOrWhiteSpace(fromHtml.Title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "BcVideo HTML/JSON extract did not include a title.");
        }

        return Task.FromResult(fromHtml);
    }

    private static string RequireVideoId(Uri url)
    {
        var videoId = ServiceCatalog.TryCompactUrl(ServiceKeys.BcVideo, url);
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "BcVideo URL did not contain a video id.");
        }

        return videoId;
    }

    private async Task<NonPodcastServiceItemMetaData?> TryVideoApiAsync(
        HttpClient client,
        Uri url,
        string videoId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, VideoApi)
            {
                Content = JsonContent.Create(new { video_id = videoId })
            };
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.Referrer = ServiceCatalog.CanonicalUrlOrSelf(ServiceKeys.BcVideo, url);
            using var timeout = new CancellationTokenSource(VideoApiRequestTimeout);
            using var response = await client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "BcVideo video-api non-success: status {StatusCode} url {Url} video-id {VideoId}.",
                    (int)response.StatusCode,
                    url,
                    videoId);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<BcVideoApiResponse>(ExtractJsonOptions);
            return MapIfTitled(payload);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(
                ex,
                "BcVideo video-api request failed for video-id {VideoId}.",
                videoId);
            return null;
        }
    }

    private static async Task<NonPodcastServiceItemMetaData?> TryOEmbedAsync(HttpClient client, Uri canonical)
    {
        try
        {
            var oEmbedUrl = new Uri(
                $"https://api.bitchute.com/oembed/?url={Uri.EscapeDataString(canonical.ToString())}&format=json");
            using var request = new HttpRequestMessage(HttpMethod.Get, oEmbedUrl);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<BcVideoOEmbedResponse>(ExtractJsonOptions);
            return MapOEmbed(payload);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private static async Task<NonPodcastServiceItemMetaData?> TryWatchHtmlAsync(HttpClient client, Uri canonical)
    {
        try
        {
            using var response = await client.GetAsync(canonical);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync();
            return ParseWatchHtml(html);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    private static NonPodcastServiceItemMetaData? TryParseExtractJson(string html)
    {
        var trimmed = html.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '{')
        {
            return null;
        }

        try
        {
            var fromApi = MapIfTitled(JsonSerializer.Deserialize<BcVideoApiResponse>(html, ExtractJsonOptions));
            if (fromApi != null)
            {
                return fromApi;
            }

            return MapOEmbed(JsonSerializer.Deserialize<BcVideoOEmbedResponse>(html, ExtractJsonOptions));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static NonPodcastServiceItemMetaData? MapIfTitled(BcVideoApiResponse? payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Title))
        {
            return null;
        }

        DateTime? release = null;
        if (!string.IsNullOrWhiteSpace(payload.DatePublished) &&
            DateTime.TryParse(
                payload.DatePublished,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            release = parsed;
        }

        Uri? image = null;
        if (!string.IsNullOrWhiteSpace(payload.ThumbnailUrl) &&
            Uri.TryCreate(payload.ThumbnailUrl, UriKind.Absolute, out var thumbnail))
        {
            image = thumbnail;
        }

        return new NonPodcastServiceItemMetaData(
            payload.Title,
            payload.Description ?? string.Empty,
            TryParseDuration(payload.Duration),
            release,
            image,
            Publisher: payload.Channel?.ChannelName);
    }

    private static NonPodcastServiceItemMetaData? MapOEmbed(BcVideoOEmbedResponse? payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Title))
        {
            return null;
        }

        Uri? image = null;
        if (!string.IsNullOrWhiteSpace(payload.ThumbnailUrl) &&
            Uri.TryCreate(payload.ThumbnailUrl, UriKind.Absolute, out var thumbnail))
        {
            image = thumbnail;
        }

        return new NonPodcastServiceItemMetaData(
            payload.Title,
            payload.Description ?? string.Empty,
            Image: image,
            Publisher: payload.AuthorName);
    }

    private static NonPodcastServiceItemMetaData? ParseWatchHtml(string html)
    {
        var title = FirstNonEmpty(
            MetaContent(html, "og:title"),
            TitleTag(html));
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var description = FirstNonEmpty(
            MetaContent(html, "og:description"),
            MetaContent(html, "description")) ?? string.Empty;

        Uri? image = null;
        var imageValue = MetaContent(html, "og:image");
        if (!string.IsNullOrWhiteSpace(imageValue) &&
            Uri.TryCreate(imageValue, UriKind.Absolute, out var imageUrl))
        {
            image = imageUrl;
        }

        return new NonPodcastServiceItemMetaData(title, description, Image: image);
    }

    private static NonPodcastServiceItemMetaData? Merge(
        NonPodcastServiceItemMetaData? oEmbed,
        NonPodcastServiceItemMetaData? html)
    {
        var title = FirstNonEmpty(oEmbed?.Title, html?.Title);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new NonPodcastServiceItemMetaData(
            title,
            FirstNonEmpty(oEmbed?.Description, html?.Description) ?? string.Empty,
            oEmbed?.Duration ?? html?.Duration,
            oEmbed?.Release ?? html?.Release,
            oEmbed?.Image ?? html?.Image,
            Publisher: FirstNonEmpty(oEmbed?.Publisher, html?.Publisher));
    }

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

            return string.IsNullOrWhiteSpace(value) ? null : WebUtility.HtmlDecode(value);
        }

        return null;
    }

    private static string? TitleTag(string html)
    {
        var match = TitleElement().Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : null;
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

    internal static TimeSpan? TryParseDuration(JsonElement duration)
    {
        if (duration.ValueKind == JsonValueKind.Number && duration.TryGetDouble(out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (duration.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var raw = duration.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var parts = raw.Trim().Split(':');
        if (parts.Length is < 1 or > 3)
        {
            return null;
        }

        var values = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]) ||
                values[i] < 0)
            {
                return null;
            }
        }

        return parts.Length switch
        {
            1 => TimeSpan.FromSeconds(values[0]),
            2 => TimeSpan.FromMinutes(values[0]) + TimeSpan.FromSeconds(values[1]),
            3 => TimeSpan.FromHours(values[0]) + TimeSpan.FromMinutes(values[1]) + TimeSpan.FromSeconds(values[2]),
            _ => null
        };
    }

    [GeneratedRegex(
        @"<meta\b(?=[^>]*\b(?:property|name)\s*=\s*[""'](?<name>[^""']+)[""'])(?=[^>]*\bcontent\s*=\s*[""'](?<content>[^""']*)[""'])[^>]*>|<meta\b(?=[^>]*\bcontent\s*=\s*[""'](?<contentAlt>[^""']*)[""'])(?=[^>]*\b(?:property|name)\s*=\s*[""'](?<nameAlt>[^""']+)[""'])[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaTag();

    [GeneratedRegex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleElement();

    private sealed class BcVideoApiResponse
    {
        [JsonPropertyName("video_name")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("duration")]
        public JsonElement Duration { get; set; }

        [JsonPropertyName("date_published")]
        public string? DatePublished { get; set; }

        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("channel")]
        public BcVideoChannel? Channel { get; set; }
    }

    private sealed class BcVideoOEmbedResponse
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("author_name")]
        public string? AuthorName { get; set; }
    }

    private sealed class BcVideoChannel
    {
        [JsonPropertyName("channel_name")]
        public string? ChannelName { get; set; }
    }
}
