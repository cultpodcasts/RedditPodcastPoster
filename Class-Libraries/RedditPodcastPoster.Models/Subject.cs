﻿using System.Text.Json.Serialization;

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
    [JsonPropertyOrder(10)]
    public string[]? Aliases { get; set; }

    [JsonPropertyName("associatedSubjects")]
    [JsonPropertyOrder(10)]
    public string[]? AssociatedSubjects { get; set; }

    public string[] GetTerms()
    {
        return
            new[] {Name}
                .Concat(Aliases ?? Array.Empty<string>())
                .Concat(AssociatedSubjects ?? Array.Empty<string>())
                .ToArray();
    }
}