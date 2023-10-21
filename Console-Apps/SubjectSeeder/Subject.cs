using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace SubjectSeeder;

public static class SubjectFactory
{
    public static Subject Create(string name, string? aliases = null, string? associatedSubjects = null)
    {
        var subject = new Subject(name);
        if (aliases != null)
        {
            subject.Aliases = aliases.Split(",").Select(x => x.Trim()).ToArray();
        }

        if (associatedSubjects != null)
        {
            subject.AssociatedSubjects = associatedSubjects.Split(",").Select(x => x.Trim()).ToArray();
        }

        return subject;
    }
}

[CosmosSelector(ModelType.Podcast)]
public sealed class Subject : CosmosSelector
{
    public static readonly string PartitionKey = ModelType.Subject.ToString();

    public Subject(string name)
        : base(Guid.NewGuid(), ModelType.Subject)
    {
        FileKey = FileKeyFactory.GetFileKey(Name);
        Name = name;
    }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(10)]
    public string Name { get; set; } = "";

    [JsonPropertyName("aliases")]
    [JsonPropertyOrder(10)]
    public string[]? Aliases { get; set; }

    [JsonPropertyName("associatedSubjects")]
    [JsonPropertyOrder(10)]
    public string[]? AssociatedSubjects { get; set; }
}