using System.Text.Json.Serialization;

using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.YouTubeQuota;

[CosmosSelector(ModelType.YouTubeIndexerKeyState)]
public sealed class YouTubeIndexerKeyState : CosmosSelector
{
    public static readonly Guid _Id = Guid.Parse("a7c3e1f4-9b2d-4f6a-8c5e-1d3f7a9b2c4e");

    public YouTubeIndexerKeyState()
    {
        Id = _Id;
        ModelType = ModelType.YouTubeIndexerKeyState;
    }

    [JsonPropertyName("pacificQuotaDate")]
    [JsonPropertyOrder(10)]
    public DateOnly PacificQuotaDate { get; set; }

    [JsonPropertyName("lastRingIndex")]
    [JsonPropertyOrder(11)]
    public int LastRingIndex { get; set; }

    [JsonPropertyName("lastApiKey")]
    [JsonPropertyOrder(12)]
    public string? LastApiKey { get; set; }

    [JsonPropertyName("updatedUtc")]
    [JsonPropertyOrder(13)]
    public DateTime UpdatedUtc { get; set; }

    public override string FileKey => nameof(YouTubeIndexerKeyState);
}
