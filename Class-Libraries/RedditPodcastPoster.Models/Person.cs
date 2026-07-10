using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.Person)]
public sealed class Person : CosmosSelector
{
    public Person(string name)
    {
        Name = name;
        Id = Guid.NewGuid();
        ModelType = ModelType.Person;
    }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(10)]
    public string Name { get; set; }

    [JsonPropertyName("aliases")]
    [JsonPropertyOrder(20)]
    public string[]? Aliases { get; set; }

    [JsonPropertyName("twitterHandle")]
    [JsonPropertyOrder(30)]
    public string? TwitterHandle { get; set; }

    [JsonPropertyName("blueskyHandle")]
    [JsonPropertyOrder(40)]
    public string? BlueskyHandle { get; set; }

    public IEnumerable<string> GetSearchTerms()
    {
        yield return Name;
        if (Aliases == null)
        {
            yield break;
        }

        foreach (var alias in Aliases.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            yield return alias.Trim();
        }
    }
}
