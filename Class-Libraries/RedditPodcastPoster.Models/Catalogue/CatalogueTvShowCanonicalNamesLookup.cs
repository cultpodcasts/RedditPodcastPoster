using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// LookUps singleton for curator Cosmo→TvShow canonical show names. Seeded from
/// <see cref="CatalogueTvShowCanonicalNames"/>; curator edits live here.
/// </summary>
[CosmosSelector(ModelType.CatalogueTvShowCanonicalNamesLookup)]
public sealed class CatalogueTvShowCanonicalNamesLookup : CosmosSelector, IFileKeyed
{
    public static readonly Guid _Id = Guid.Parse("A1056003-2B3C-4D5E-9F70-8192A3B4C5D6");

    public CatalogueTvShowCanonicalNamesLookup()
    {
        Id = _Id;
        ModelType = ModelType.CatalogueTvShowCanonicalNamesLookup;
    }

    /// <summary>Publisher (podcast) name → canonical TvShow name.</summary>
    [JsonPropertyName("showNameByPublisherName")]
    [JsonPropertyOrder(10)]
    public Dictionary<string, string> ShowNameByPublisherName { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(CatalogueTvShowCanonicalNamesLookup);

    public static CatalogueTvShowCanonicalNamesLookup CreateFromSeed() => new()
    {
        ShowNameByPublisherName = new Dictionary<string, string>(
            CatalogueTvShowCanonicalNames.ShowNameByPublisherName,
            StringComparer.OrdinalIgnoreCase)
    };
}
