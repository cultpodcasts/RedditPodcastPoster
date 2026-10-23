using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoverySubmitRequest
{
    [JsonPropertyName("ids")]
    public Guid[] DiscoveryResultsDocumentIds { get; set; } = [];

    [JsonPropertyName("resultIds")]
    public Guid[] ResultIds { get; set; } = [];

    /// <summary>
    /// Answers for items whose detected language is not supported yet. Keys are discovery result ids;
    /// values are <c>add</c> or <c>continue</c>. Omitted ids have not been answered. Cancelled ids are left out of
    /// <see cref="ResultIds"/>.
    /// </summary>
    [JsonPropertyName("languageDecisions")]
    public Dictionary<Guid, string>? LanguageDecisions { get; set; }

    /// <summary>
    /// Answers for items whose authority search matched several programmes. Keys are discovery result ids;
    /// values are <c>select</c> or <c>continue</c>. With <c>select</c>, also send <see cref="SelectedIdentityKeys"/>.
    /// </summary>
    [JsonPropertyName("identityDecisions")]
    public Dictionary<Guid, string>? IdentityDecisions { get; set; }

    [JsonPropertyName("selectedIdentityKeys")]
    public Dictionary<Guid, string>? SelectedIdentityKeys { get; set; }
}
