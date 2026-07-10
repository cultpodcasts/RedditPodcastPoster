using System.Text.Json.Serialization;

namespace EpisodeGuestHandleRestorer;

public sealed class BackupEpisodeDocument
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("podcastId")]
    public Guid PodcastId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("twitterHandles")]
    public string[]? TwitterHandles { get; set; }

    [JsonPropertyName("blueskyHandles")]
    public string[]? BlueskyHandles { get; set; }

    public bool HasGuestHandles =>
        TwitterHandles is { Length: > 0 } || BlueskyHandles is { Length: > 0 };
}

public sealed class ProductionEpisodeDocument
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("podcastId")]
    public Guid PodcastId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("twitterHandles")]
    public string[]? TwitterHandles { get; set; }

    [JsonPropertyName("blueskyHandles")]
    public string[]? BlueskyHandles { get; set; }
}

public sealed record HandlePatchPlan(
    Guid EpisodeId,
    string? Title,
    string[]? ProdTwitterHandles,
    string[]? ProdBlueskyHandles,
    string[]? BackupTwitterHandles,
    string[]? BackupBlueskyHandles,
    bool PatchTwitter,
    bool PatchBluesky)
{
    public string[]? TwitterToSet => PatchTwitter ? BackupTwitterHandles : null;
    public string[]? BlueskyToSet => PatchBluesky ? BackupBlueskyHandles : null;
}
