using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Vimeo.Extractors;

public interface IVimeoMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class VimeoMetaDataExtractor(IHttpClientFactory httpClientFactory) : IVimeoMetaDataExtractor
{
    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var oEmbedUrl = new Uri(
            $"https://vimeo.com/api/oembed.json?url={Uri.EscapeDataString(url.ToString())}");
        var client = httpClientFactory.CreateClient(nameof(VimeoMetaDataExtractor));
        using var response = await client.GetAsync(oEmbedUrl);
        if (!response.IsSuccessStatusCode)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<VimeoOEmbedResponse>()
                      ?? throw new NonPodcastServiceMetaDataExtractionException(url, "Empty Vimeo oEmbed payload.");
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, "Vimeo oEmbed did not include a title.");
        }

        DateTime? release = null;
        if (!string.IsNullOrWhiteSpace(payload.UploadDate) &&
            DateTime.TryParse(
                payload.UploadDate,
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

        TimeSpan? duration = payload.Duration is > 0
            ? TimeSpan.FromSeconds(payload.Duration.Value)
            : null;

        return new NonPodcastServiceItemMetaData(
            payload.Title,
            payload.Description ?? string.Empty,
            duration,
            release,
            image,
            Publisher: payload.AuthorName);
    }

    private sealed class VimeoOEmbedResponse
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("duration")]
        public int? Duration { get; set; }

        [JsonPropertyName("upload_date")]
        public string? UploadDate { get; set; }

        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("author_name")]
        public string? AuthorName { get; set; }
    }
}
