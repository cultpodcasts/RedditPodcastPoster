using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.PushSubscription)]
public sealed class PushSubscription : CosmosSelector
{
    public PushSubscription(Uri endpoint, DateTime? expirationTime, string auth, string p256dh, string user)
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.PushSubscription;
        Endpoint = endpoint;
        ExpirationTime = expirationTime;
        Auth = auth;
        P256Dh = p256dh;
        User = user;
    }

    [JsonPropertyName("user")]
    [JsonPropertyOrder(10)]
    public string User { get; set; }

    [JsonPropertyName("endpoint")]
    [JsonPropertyOrder(20)]
    public Uri Endpoint { get; set; }

    [JsonPropertyName("expirationTime")]
    [JsonPropertyOrder(30)]
    public DateTime? ExpirationTime { get; set; }

    [JsonPropertyName("auth")]
    [JsonPropertyOrder(40)]
    public string Auth { get; set; }

    [JsonPropertyName("p256dh")]
    [JsonPropertyOrder(50)]
    public string P256Dh { get; set; }
}