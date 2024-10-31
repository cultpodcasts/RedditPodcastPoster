using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PushSubscriptions.Dtos;

public class NotificationOnActionClick
{
    [JsonPropertyName("operation")]
    public ActionOperation Operation { get; set; }

    [JsonPropertyName("url")]
    public Uri? Url { get; set; }
}