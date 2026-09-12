// pragma: allowlist secret
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Models; // pragma: allowlist secret

namespace RedditPodcastPoster.BitChute.Extractors; // pragma: allowlist secret

public interface IBitChuteMetaDataExtractor // pragma: allowlist secret
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url); // pragma: allowlist secret
}

public class BitChuteMetaDataExtractor(IHttpClientFactory httpClientFactory) : IBitChuteMetaDataExtractor // pragma: allowlist secret
{
    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url) // pragma: allowlist secret
    {
        var oEmbedUrl = new Uri(
            $"https://api.bitchute.com/oembed/?url={Uri.EscapeDataString(url.ToString())}&format=json"); // pragma: allowlist secret
        var client = httpClientFactory.CreateClient(nameof(BitChuteMetaDataExtractor)); // pragma: allowlist secret
        using var response = await client.GetAsync(oEmbedUrl);
        if (!response.IsSuccessStatusCode)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, response.StatusCode); // pragma: allowlist secret
        }

        var payload = await response.Content.ReadFromJsonAsync<BitChuteOEmbedResponse>() // pragma: allowlist secret
                      ?? throw new NonPodcastServiceMetaDataExtractionException(url, "Empty BitChute oEmbed payload."); // pragma: allowlist secret
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, "BitChute oEmbed did not include a title."); // pragma: allowlist secret
        }

        Uri? image = null;
        if (!string.IsNullOrWhiteSpace(payload.ThumbnailUrl) &&
            Uri.TryCreate(payload.ThumbnailUrl, UriKind.Absolute, out var thumbnail))
        {
            image = thumbnail;
        }

        return new NonPodcastServiceItemMetaData( // pragma: allowlist secret
            payload.Title,
            payload.Description ?? string.Empty,
            Image: image,
            Publisher: payload.AuthorName);
    }

    private sealed class BitChuteOEmbedResponse // pragma: allowlist secret
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
