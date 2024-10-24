using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.PushSubscription)]
public sealed class PushSubscription : CosmosSelector
{
    public PushSubscription(Uri endpoint, DateTime? expirationTime, string auth, string p256dh)
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.PushSubscription;
        Endpoint = endpoint;
        ExpirationTime = expirationTime;
        Auth = auth;
        P256dh = p256dh;
    }

    [JsonPropertyName("endpoint")]
    [JsonPropertyOrder(10)]
    public Uri Endpoint { get; set; }

    [JsonPropertyName("expirationTime")]
    [JsonPropertyOrder(11)]
    public DateTime? ExpirationTime { get; set; }

    [JsonPropertyName("auth")]
    [JsonPropertyOrder(12)]
    public string Auth { get; set; }

    [JsonPropertyName("p256dh")]
    [JsonPropertyOrder(13)]
    public string P256dh { get; set; }
}