using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.BcVideo.Extractors;

public interface IBcVideoMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class BcVideoMetaDataExtractor(IHttpClientFactory httpClientFactory) : IBcVideoMetaDataExtractor
{
    private static readonly Uri VideoApi = new("https://api.bitchute.com/api/beta/video");

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var videoId = ServiceCatalog.TryCompactUrl(ServiceKeys.BcVideo, url);
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "BcVideo URL did not contain a video id.");
        }

        var client = httpClientFactory.CreateClient(nameof(BcVideoMetaDataExtractor));
        using var response = await client.PostAsJsonAsync(VideoApi, new { video_id = videoId });
        if (!response.IsSuccessStatusCode)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<BcVideoApiResponse>()
                      ?? throw new NonPodcastServiceMetaDataExtractionException(url, "Empty BcVideo video payload.");
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, "BcVideo video JSON did not include a title.");
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
            2 => new TimeSpan(0, values[0], values[1]),
            3 => new TimeSpan(values[0], values[1], values[2]),
            _ => null
        };
    }

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

    private sealed class BcVideoChannel
    {
        [JsonPropertyName("channel_name")]
        public string? ChannelName { get; set; }
    }
}
