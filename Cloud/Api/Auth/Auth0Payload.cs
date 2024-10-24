using System.Text.Json.Serialization;

namespace Api.Auth;

public class Auth0Payload
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [JsonPropertyName("https://api.cultpodcasts.com/roles")]
    public string[] Roles { get; set; } = [];

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; set; } = [];

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "";

    [JsonPropertyName("azp")]
    public string Azp { get; set; } = "";

    [JsonPropertyName("iss")]
    public string Issuer { get; set; } = "";

    [JsonPropertyName("sub")]
    public string Subject { get; set; } = "";

    [JsonPropertyName("aud")]
    public string[] Audience { get; set; } = [];

    [JsonPropertyName("exp")]
    public long? ExpirationTimeSeconds { get; set; }

    [JsonPropertyName("iat")]
    public long? IssuedAtTimeSeconds { get; set; }

    [JsonIgnore]
    public DateTimeOffset? IssuedAt =>
        IssuedAtTimeSeconds is null ? null : UnixEpoch.AddSeconds(IssuedAtTimeSeconds.Value);

    [JsonIgnore]
    public DateTimeOffset? ExpiresAt =>
        ExpirationTimeSeconds is null ? null : UnixEpoch.AddSeconds(ExpirationTimeSeconds.Value);
}