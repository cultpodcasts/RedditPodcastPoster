using System.Text.Json.Serialization;

namespace GuestHandleRecovery;

public sealed class BackupEpisodeDocument
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("podcastId")]
    public Guid PodcastId { get; set; }

    [JsonPropertyName("twitterHandles")]
    public string[]? TwitterHandles { get; set; }

    [JsonPropertyName("blueskyHandles")]
    public string[]? BlueskyHandles { get; set; }

    [JsonPropertyName("guests")]
    public string[]? Guests { get; set; }

    public bool HasGuestHandles =>
        TwitterHandles is { Length: > 0 } || BlueskyHandles is { Length: > 0 };
}

public sealed class ProductionEpisodeDocument
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("podcastId")]
    public Guid PodcastId { get; set; }

    [JsonPropertyName("twitterHandles")]
    public string[]? TwitterHandles { get; set; }

    [JsonPropertyName("blueskyHandles")]
    public string[]? BlueskyHandles { get; set; }

    [JsonPropertyName("guests")]
    public string[]? Guests { get; set; }

    public bool HasGuestHandles =>
        TwitterHandles is { Length: > 0 } || BlueskyHandles is { Length: > 0 };
}
