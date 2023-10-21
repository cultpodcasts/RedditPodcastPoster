﻿using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Text.KnownTerms;

[CosmosSelector(ModelType.EliminationTerms)]
public class KnownTerms : CosmosSelector
{
    public static Guid _Id = Guid.Parse("4B550528-6848-4877-A768-0DA301754C7B");

    public static readonly string PartitionKey = ModelType.KnownTerms.ToString();

    public KnownTerms() : base(_Id, ModelType.KnownTerms)
    {
    }

    [JsonPropertyName("terms")]
    [JsonPropertyOrder(10)]
    public Dictionary<string, Regex> Terms { get; set; } = new();

    public string MaintainKnownTerms(string input)
    {
        foreach (var term in Terms)
        {
            input = term.Value.Replace(input, term.Key);
        }

        return input;
    }

    public override string FileKey=>nameof(KnownTerms);
}