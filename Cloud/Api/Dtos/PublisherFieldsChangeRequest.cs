using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Publisher fields a curator can PATCH on a news organisation, TV show or film. Omitted (null) = unchanged.
/// Strings: "" clears. Arrays: [] clears. Same rules as the podcast change request.
/// <c>language</c> "" or English is stored as null. Name is not part of PATCH; it has its own rename endpoint (<c>POST {kind}/name/{identifier}</c>) that records a redirect.
/// </summary>
public abstract class PublisherFieldsChangeRequest
{
    /// <summary>Network / label / brand attribution (Cosmos <c>publisher</c>). Trimmed; "" clears.</summary>
    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    /// <summary>"" allowed.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// News organisation / TV show: cascades to the children (sets or clears their <c>parentRemoved</c>, and on
    /// removal deletes their search documents). Film: deletes the film's own search document on removal.
    /// </summary>
    [JsonPropertyName("removed")]
    public bool? Removed { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("twitterHandle")]
    public string? TwitterHandle { get; set; }

    [JsonPropertyName("blueskyHandle")]
    public string? BlueskyHandle { get; set; }

    [JsonPropertyName("hashTag")]
    public string? HashTag { get; set; }

    [JsonPropertyName("enrichmentHashTags")]
    public string[]? EnrichmentHashTags { get; set; }

    [JsonPropertyName("searchTerms")]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("knownTerms")]
    public string[]? KnownTerms { get; set; }

    [JsonPropertyName("defaultSubject")]
    public string? DefaultSubject { get; set; }

    [JsonPropertyName("ignoredSubjects")]
    public string[]? IgnoredSubjects { get; set; }

    [JsonPropertyName("ignoredAssociatedSubjects")]
    public string[]? IgnoredAssociatedSubjects { get; set; }
}
