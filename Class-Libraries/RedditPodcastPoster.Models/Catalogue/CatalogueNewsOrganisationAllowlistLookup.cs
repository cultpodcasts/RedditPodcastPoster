using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// LookUps singleton for S-008 NewsOrganisation apply allowlist. Seeded from
/// <see cref="CatalogueNewsOrganisationAllowlist"/>; curator edits live here.
/// </summary>
[CosmosSelector(ModelType.CatalogueNewsOrganisationAllowlistLookup)]
public sealed class CatalogueNewsOrganisationAllowlistLookup : CosmosSelector, IFileKeyed
{
    public static readonly Guid _Id = Guid.Parse("A1056001-8F3A-4B2C-9D4E-F5A6B7C8D9E0");

    public CatalogueNewsOrganisationAllowlistLookup()
    {
        Id = _Id;
        ModelType = ModelType.CatalogueNewsOrganisationAllowlistLookup;
    }

    [JsonPropertyName("names")]
    [JsonPropertyOrder(10)]
    public List<string> Names { get; set; } = [];

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(CatalogueNewsOrganisationAllowlistLookup);

    public static CatalogueNewsOrganisationAllowlistLookup CreateFromSeed() => new()
    {
        Names = CatalogueNewsOrganisationAllowlist.Names
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList()
    };
}
