using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.Subject)]
public sealed class Subject : CosmosSelector
{
    public static readonly string PartitionKey = ModelType.Subject.ToString();

    public Subject(string name)
        : base(Guid.NewGuid(), ModelType.Subject)
    {
        Name = name;
    }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(10)]
    public string Name { get; set; } = "";

    [JsonPropertyName("aliases")]
    [JsonPropertyOrder(20)]
    public string[]? Aliases { get; set; }

    [JsonPropertyName("associatedSubjects")]
    [JsonPropertyOrder(30)]
    public string[]? AssociatedSubjects { get; set; }

    [JsonPropertyName("redditFlairTemplateId")]
    [JsonPropertyOrder(40)]
    public Guid? RedditFlairTemplateId { get; set; }

    [JsonPropertyName("hashtag")]
    [JsonPropertyOrder(50)]
    public string? HashTag { get; set; }

    public SubjectTerm[] GetSubjectTerms()
    {
        return
            new[] {new SubjectTerm(Name, SubjectTermType.Name)}
                .Concat(Aliases != null
                    ? Aliases.Select(term => new SubjectTerm(term, SubjectTermType.Alias))
                    : Array.Empty<SubjectTerm>())
                .Concat(AssociatedSubjects != null
                    ? AssociatedSubjects.Select(term => new SubjectTerm(term, SubjectTermType.AssociatedSubject))
                    : Array.Empty<SubjectTerm>())
                .ToArray();
    }
}