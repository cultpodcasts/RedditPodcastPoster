using System.Text.Json.Serialization;

namespace Api;

public class ClientPrincipal
{
    [JsonPropertyName("auth_typ")]
    public string IdentityProvider { get; set; } = "";

    [JsonPropertyName("name_typ")]
    public string NameClaimType { get; set; } = "";

    [JsonPropertyName("role_typ")]
    public string RoleClaimType { get; set; } = "";

    [JsonPropertyName("claims")]
    public IEnumerable<ClientPrincipalClaim> Claims { get; set; } = Enumerable.Empty<ClientPrincipalClaim>();
}