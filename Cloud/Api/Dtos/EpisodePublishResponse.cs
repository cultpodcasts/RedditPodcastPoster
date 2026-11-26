using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodePublishResponse(Guid PodcastId)
{
    [JsonPropertyName("posted")]
    public bool? Posted { get; set; }

    [JsonPropertyName("tweeted")]
    public bool? Tweeted { get; set; }

    [JsonPropertyName("blueskyPosted")]
    public bool? BlueskyPosted { get; set; }

    [JsonPropertyName("failedTweetContent")]
    public string? FailedTweetContent { get; set; }

    [JsonPropertyName("podcastId")]
    public Guid? PodcastId { get; init; } = PodcastId;

    /// <summary>Why nothing was published, on a refused request (e.g. held for approval, #1484).</summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    public bool Updated()
    {
        return (Posted.HasValue && Posted.Value) ||
               (Tweeted.HasValue && Tweeted.Value) ||
               (BlueskyPosted.HasValue && BlueskyPosted.Value);
    }
}