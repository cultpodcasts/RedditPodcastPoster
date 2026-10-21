using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Publisher fields returned by the curator GET for a news organisation, TV show or film.</summary>
public abstract class PublisherFieldsDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("publisher")]
    public string PublisherName { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("removed")]
    public bool Removed { get; set; }

    [JsonPropertyName("twitterHandle")]
    public string TwitterHandle { get; set; } = "";

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

    [JsonPropertyName("latestReleased")]
    public DateTime? LatestReleased { get; set; }

    [JsonPropertyName("lastIndexed")]
    public DateTime? LastIndexed { get; set; }
}
