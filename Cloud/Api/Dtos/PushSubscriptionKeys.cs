using System.Text.Json.Serialization;

namespace Api.Dtos;

public class PushSubscriptionKeys
{
    [JsonPropertyName("auth")]
    public required string Auth { get; set; }

    [JsonPropertyName("p256dh")]
    public required string P256dh { get; set; }
}