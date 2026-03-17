using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Reddit;

public class DevvitEpisodeCreateResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("postType")]
    public string PostType { get; set; } = "";

    [JsonPropertyName("postId")]
    public string PostId { get; set; } = "";

    [JsonPropertyName("postUrl")]
    public string PostUrl { get; set; } = "";
}
