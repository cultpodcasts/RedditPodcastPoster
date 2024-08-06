using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace Api.Dtos;

public class Subject
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("aliases")]
    public string[]? Aliases { get; set; }

    [JsonPropertyName("associatedSubjects")]
    public string[]? AssociatedSubjects { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("enrichmentHashTags")]
    public string[]? EnrichmentHashTags { get; set; }

    [JsonPropertyName("hashTag")]
    public string? HashTag { get; set; }

    [JsonPropertyName("redditFlairTemplateId")]
    public Guid? RedditFlairTemplateId { get; set; }

    [JsonPropertyName("redditFlareText")]
    public string? RedditFlareText { get; set; }

    [JsonPropertyName("subjectType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubjectType? SubjectType { get; set; }
}