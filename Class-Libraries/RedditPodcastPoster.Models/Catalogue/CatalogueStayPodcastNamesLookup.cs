using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// LookUps singleton for curator stay-podcast publisher names. Seeded from
/// <see cref="CatalogueStayPodcastNames"/>; curator edits live here.
/// </summary>
[CosmosSelector(ModelType.CatalogueStayPodcastNamesLookup)]
public sealed class CatalogueStayPodcastNamesLookup : CosmosSelector, IFileKeyed
{
    public static readonly Guid _Id = Guid.Parse("A1056002-1A2B-4C3D-8E5F-60718293A4B5");

    public CatalogueStayPodcastNamesLookup()
    {
        Id = _Id;
        ModelType = ModelType.CatalogueStayPodcastNamesLookup;
    }

    [JsonPropertyName("names")]
    [JsonPropertyOrder(10)]
    public List<string> Names { get; set; } = [];

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(CatalogueStayPodcastNamesLookup);

    public static CatalogueStayPodcastNamesLookup CreateFromSeed() => new()
    {
        Names = CatalogueStayPodcastNames.Names
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList()
    };
}
