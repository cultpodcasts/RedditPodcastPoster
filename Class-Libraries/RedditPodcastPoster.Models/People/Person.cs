using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.People;

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

    /// <summary>
    /// Optional curator override for surname-style ordering.
    /// When null/blank, <see cref="GetEffectiveSortKey()"/> derives the last whitespace-separated token of <see cref="Name"/>.
    /// </summary>
    [JsonPropertyName("sortName")]
    [JsonPropertyOrder(12)]
    public string? SortName { get; set; }

    /// <summary>
    /// When true, this entry is an organization/entity: sort using the full name
    /// (see <c>sortName</c> / <see cref="GetEffectiveSortKey()"/>) rather than a surname token.
    /// </summary>
    [JsonPropertyName("isOrganization")]
    [JsonPropertyOrder(13)]
    public bool IsOrganization { get; set; }

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

    /// <summary>
    /// Last whitespace-separated token of <paramref name="name"/> (trimmed).
    /// Hyphenated tokens are kept whole (e.g. Smith-Jones).
    /// </summary>
    public static string DeriveSortKeyFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var parts = name.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[^1];
    }

    public static string GetEffectiveSortKey(string? name, string? sortName) =>
        !string.IsNullOrWhiteSpace(sortName) ? sortName.Trim() : DeriveSortKeyFromName(name);

    public string GetEffectiveSortKey() => GetEffectiveSortKey(Name, SortName);

    public IEnumerable<string> GetNames()
    {
        if (!string.IsNullOrWhiteSpace(Name))
        {
            yield return Name.Trim();
        }

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
