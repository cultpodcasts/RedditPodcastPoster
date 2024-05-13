using System.Text.Json.Serialization;

namespace Api;

public class ClientPrincipalClaim
{
    [JsonPropertyName("typ")]
    public string Type { get; set; } = "";

    [JsonPropertyName("val")]
    public string Value { get; set; } = "";
}