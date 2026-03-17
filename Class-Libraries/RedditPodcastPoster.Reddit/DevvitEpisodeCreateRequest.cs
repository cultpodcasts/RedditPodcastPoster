using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Reddit;

public class DevvitEpisodeCreateRequest
{
    [JsonPropertyName("podcastName")]
    public string PodcastName { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("releaseDateTime")]
    public string ReleaseDateTime { get; set; } = "";

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = "";

    [JsonPropertyName("subredditName")]
    public string? SubredditName { get; set; }

    [JsonPropertyName("flairId")]
    public string? FlairId { get; set; }

    [JsonPropertyName("flairText")]
    public string? FlairText { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("serviceLinks")]
    public DevvitServiceLinks ServiceLinks { get; set; } = new();
}

public class DevvitServiceLinks
{
    [JsonPropertyName("youtube")]
    public string? Youtube { get; set; }

    [JsonPropertyName("spotify")]
    public string? Spotify { get; set; }

    [JsonPropertyName("apple_podcasts")]
    public string? ApplePodcasts { get; set; }
}
