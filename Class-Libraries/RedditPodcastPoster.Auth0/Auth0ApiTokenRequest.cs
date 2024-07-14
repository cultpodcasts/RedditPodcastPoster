using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Auth0;

public class Auth0ApiTokenRequest
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; set; }

    [JsonPropertyName("client_secret")]
    public required string ClientSecret { get; set; }

    [JsonPropertyName("audience")]
    public required string Audience { get; set; }

    [JsonPropertyName("grant_type")]
    public required string GrantType { get; set; }
}