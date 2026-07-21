using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Auth0.Models;

public class ClientPrincipalClaim
{
    [JsonPropertyName("typ")]
    public string Type { get; set; } = "";

    [JsonPropertyName("val")]
    public string Value { get; set; } = "";
}
