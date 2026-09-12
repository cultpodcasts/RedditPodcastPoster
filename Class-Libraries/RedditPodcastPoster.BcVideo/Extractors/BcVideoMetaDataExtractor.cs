using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.BcVideo.Extractors;

public interface IBcVideoMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class BcVideoMetaDataExtractor(IHttpClientFactory httpClientFactory) : IBcVideoMetaDataExtractor
{
    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var oEmbedUrl = new Uri(
            $"https://api.bitchute.com/oembed/?url={Uri.EscapeDataString(url.ToString())}&format=json");
        var client = httpClientFactory.CreateClient(nameof(BcVideoMetaDataExtractor));
        using var response = await client.GetAsync(oEmbedUrl);
        if (!response.IsSuccessStatusCode)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<BcVideoOEmbedResponse>()
                      ?? throw new NonPodcastServiceMetaDataExtractionException(url, "Empty BcVideo oEmbed payload.");
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, "BcVideo oEmbed did not include a title.");
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
}
