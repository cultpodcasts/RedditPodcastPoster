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

    public bool Updated()
    {
        return (Posted.HasValue && Posted.Value) ||
               (Tweeted.HasValue && Tweeted.Value) ||
               (BlueskyPosted.HasValue && BlueskyPosted.Value);
    }
}