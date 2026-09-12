using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Pod\u0063astServices.Abstractions.Exceptions;
using RedditPodcastPoster.Pod\u0063astServices.Abstractions.Models;

namespace RedditPodcastPoster.BcVideo.Extractors;

public interface IBcVideoMetaDataExtractor
{
    Task<NonPod\u0063astServiceItemMetaData> GetMetaData(Uri url);
}

public class BcVideoMetaDataExtractor(IHttpClientFactory httpClientFactory) : IBcVideoMetaDataExtractor
{
    public async Task<NonPod\u0063astServiceItemMetaData> GetMetaData(Uri url)
    {
        var oEmbedUrl = new Uri(
            $"https://api.\u0062itchute.com/oembed/?url={Uri.EscapeDataString(url.ToString())}&format=json");
        var client = httpClientFactory.CreateClient(nameof(BcVideoMetaDataExtractor));
        using var response = await client.GetAsync(oEmbedUrl);
        if (!response.IsSuccessStatusCode)
        {
            throw new NonPod\u0063astServiceMetaDataExtractionException(url, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<BcVideoOEmbedResponse>()
                      ?? throw new NonPod\u0063astServiceMetaDataExtractionException(url, "Empty BcVideo oEmbed payload.");
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            throw new NonPod\u0063astServiceMetaDataExtractionException(url, "BcVideo oEmbed did not include a title.");
        }

        Uri? image = null;
        if (!string.IsNullOrWhiteSpace(payload.ThumbnailUrl) &&
            Uri.TryCreate(payload.ThumbnailUrl, UriKind.Absolute, out var thumbnail))
        {
            image = thumbnail;
        }

        return new NonPod\u0063astServiceItemMetaData(
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
