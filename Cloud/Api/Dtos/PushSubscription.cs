using System.Text.Json.Serialization;

namespace Api.Dtos;

public class PushSubscription
{
    [JsonPropertyName("endpoint")]
    public required Uri Endpoint { get; set; }

    [JsonPropertyName("expirationTime")]
    public long? ExpirationTime { get; set; }

    [JsonPropertyName("keys")]
    public required PushSubscriptionKeys Keys { get; set; }
}