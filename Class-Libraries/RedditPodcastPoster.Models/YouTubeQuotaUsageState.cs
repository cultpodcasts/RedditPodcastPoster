using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.YouTubeQuotaUsageState)]
public sealed class YouTubeQuotaUsageState : CosmosSelector
{
    public static readonly Guid _Id = Guid.Parse("b8d4e2f5-0c3a-4d7e-9a1b-2c5f8e3d6a7b");

    public YouTubeQuotaUsageState()
    {
        Id = _Id;
        ModelType = ModelType.YouTubeQuotaUsageState;
    }

    [JsonPropertyName("pacificQuotaDate")]
    [JsonPropertyOrder(10)]
    public DateOnly PacificQuotaDate { get; set; }

    [JsonPropertyName("sourceApplication")]
    [JsonPropertyOrder(11)]
    public required string SourceApplication { get; set; }

    [JsonPropertyName("entries")]
    [JsonPropertyOrder(12)]
    public List<YouTubeQuotaUsageEntry> Entries { get; set; } = [];

    [JsonPropertyName("updatedUtc")]
    [JsonPropertyOrder(13)]
    public DateTime UpdatedUtc { get; set; }

    public override string FileKey => nameof(YouTubeQuotaUsageState);
}

public sealed class YouTubeQuotaUsageEntry
{
    [JsonPropertyName("statsKey")]
    [JsonPropertyOrder(10)]
    public required string StatsKey { get; set; }

    [JsonPropertyName("displayName")]
    [JsonPropertyOrder(11)]
    public required string DisplayName { get; set; }

    [JsonPropertyName("project")]
    [JsonPropertyOrder(12)]
    public required string Project { get; set; }

    [JsonPropertyName("usage")]
    [JsonPropertyOrder(13)]
    public required string Usage { get; set; }

    [JsonPropertyName("callsAttempted")]
    [JsonPropertyOrder(14)]
    public int CallsAttempted { get; set; }

    [JsonPropertyName("quotaHits")]
    [JsonPropertyOrder(15)]
    public int QuotaHits { get; set; }

    [JsonPropertyName("quotaUsed")]
    [JsonPropertyOrder(16)]
    public int QuotaUsed { get; set; }
}
