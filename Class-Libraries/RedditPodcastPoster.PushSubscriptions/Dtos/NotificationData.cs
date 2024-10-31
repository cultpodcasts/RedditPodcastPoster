using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PushSubscriptions.Dtos;

public class NotificationData
{
    [JsonPropertyName("onActionClick")]
    public Dictionary<string, NotificationOnActionClick>? OnActionClick { get; set; }
}