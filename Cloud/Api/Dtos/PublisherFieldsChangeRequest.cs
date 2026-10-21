using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Publisher fields a curator can PATCH on a news organisation, TV show or film. Omitted (null) = unchanged.
/// Strings: "" clears. Arrays: [] clears. Same rules as the podcast change request.
/// <c>language</c> "" or English is stored as null. Name and removed are not editable here.
/// </summary>
public abstract class PublisherFieldsChangeRequest
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
