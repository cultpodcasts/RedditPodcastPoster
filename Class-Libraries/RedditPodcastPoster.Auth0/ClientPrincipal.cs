using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Auth0;

public class ClientPrincipal
{
    public const string ClaimsNameIdentifierType =
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";

    [JsonPropertyName("auth_typ")]
    public string IdentityProvider { get; set; } = "";

    [JsonPropertyName("name_typ")]
    public string NameClaimType { get; set; } = "";

    [JsonPropertyName("role_typ")]
    public string RoleClaimType { get; set; } = "";

    [JsonPropertyName("claims")]
    public IEnumerable<ClientPrincipalClaim> Claims { get; set; } = [];

    [JsonIgnore]
    public string? Subject =>
        Claims.SingleOrDefault(x => x.Type == ClaimsNameIdentifierType)?.Value;

    public bool HasScope(string scope)
    {
        var scopeClaim = Claims.SingleOrDefault(x => x.Type == "permissions" && x.Value == scope);
        return scopeClaim != null;
    }
}