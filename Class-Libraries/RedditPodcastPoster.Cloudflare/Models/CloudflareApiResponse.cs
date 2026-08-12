using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Cloudflare.Models;

public class CloudflareApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }
}
