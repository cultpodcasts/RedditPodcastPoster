using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Outcome of an edit-time Identity tab authority lookup.</summary>
public enum CatalogueIdentityLookupStatus
{
    Matched,
    Ambiguous,
    NoMatch
}

public class CatalogueIdentityLookupResponse
{
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required CatalogueIdentityLookupStatus Status { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("ids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CatalogueIdentityIdsBody? Ids { get; set; }

    /// <summary>When looking up a TV episode, ids collected for the parent show (fill blanks only).</summary>
    [JsonPropertyName("parentIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CatalogueIdentityIdsBody? ParentIds { get; set; }

    /// <summary>Present when <see cref="Status"/> is <see cref="CatalogueIdentityLookupStatus.Ambiguous"/> — same shape as submit identityDecision.</summary>
    [JsonPropertyName("identityDecision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubmitUrlResponse.IdentityDecisionBody? IdentityDecision { get; set; }

    public static CatalogueIdentityLookupResponse Matched(
        CatalogueIdentityIdsBody ids,
        CatalogueIdentityIdsBody? parentIds = null) =>
        new() { Status = CatalogueIdentityLookupStatus.Matched, Ids = ids, ParentIds = parentIds };

    public static CatalogueIdentityLookupResponse Ambiguous(SubmitUrlResponse.IdentityDecisionBody decision) =>
        new() { Status = CatalogueIdentityLookupStatus.Ambiguous, IdentityDecision = decision };

    public static CatalogueIdentityLookupResponse NoMatch(string message) =>
        new() { Status = CatalogueIdentityLookupStatus.NoMatch, Message = message };
}

public class CatalogueIdentityIdsBody
{
    [JsonPropertyName("imdbId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tmdbId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TmdbId { get; set; }

    [JsonPropertyName("tvdbId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TvdbId { get; set; }
}
