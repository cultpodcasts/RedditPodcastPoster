using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PushSubscriptions.Dtos;

public class NotificationAction
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}