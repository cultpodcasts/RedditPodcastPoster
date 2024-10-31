using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PushSubscriptions.Dtos;

public class Notification
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("actions")]
    public List<NotificationAction>? Actions { get; set; }

    [JsonPropertyName("badge")]
    public string? Badge { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("renotify")]
    public bool? Renotify { get; set; }

    [JsonPropertyName("requireInteraction")]
    public bool? RequireInteraction { get; set; }

    [JsonPropertyName("silent")]
    public bool? Silent { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }

    [JsonPropertyName("vibrate")]
    public int[]? Vibrate { get; set; }

    [JsonPropertyName("data")]
    public NotificationData? Data { get; set; }
}