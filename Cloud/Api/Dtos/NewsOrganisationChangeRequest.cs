using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// PATCH body for <c>newsorganisation/{id}</c>. Omitted (null) = unchanged.
/// Strings: "" clears. Arrays: [] clears. Same rules as the podcast change request.
/// </summary>
public class NewsOrganisationChangeRequest
{
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
