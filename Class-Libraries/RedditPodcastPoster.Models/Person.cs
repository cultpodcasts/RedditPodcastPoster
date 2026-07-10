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
        EnsureNameKey();
    }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(10)]
    public string Name { get; set; }

    /// <summary>
    /// Case-insensitive uniqueness key for <see cref="Name"/> (trimmed + lower-invariant).
    /// Cosmos unique keys are case-sensitive on the stored value, so this field is what
    /// UniqueKeyPolicy and app-level conflict checks use.
    /// </summary>
    [JsonPropertyName("nameKey")]
    [JsonPropertyOrder(11)]
    public string NameKey { get; set; } = string.Empty;

    [JsonPropertyName("aliases")]
    [JsonPropertyOrder(20)]
    public string[]? Aliases { get; set; }

    [JsonPropertyName("twitterHandle")]
    [JsonPropertyOrder(30)]
    public string? TwitterHandle { get; set; }

    [JsonPropertyName("blueskyHandle")]
    [JsonPropertyOrder(40)]
    public string? BlueskyHandle { get; set; }

    public static string NormalizeNameKey(string? name) =>
        string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim().ToLowerInvariant();

    public void EnsureNameKey()
    {
        NameKey = NormalizeNameKey(Name);
    }

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
