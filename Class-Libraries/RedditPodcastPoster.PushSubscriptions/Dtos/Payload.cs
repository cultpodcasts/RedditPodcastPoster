using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PushSubscriptions.Dtos;

public class Payload
{
    [JsonPropertyName("notification")]
    public  Notification? Notification { get; set; }

}