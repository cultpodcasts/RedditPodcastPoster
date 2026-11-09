using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// LookUps singleton for extra YouTube channel → canonical news organisation names.
/// Seeded from <see cref="CatalogueNewsOrganisationChannelAliases"/>; curator edits live here.
/// </summary>
[CosmosSelector(ModelType.CatalogueNewsOrganisationChannelAliasesLookup)]
public sealed class CatalogueNewsOrganisationChannelAliasesLookup : CosmosSelector, IFileKeyed
{
    public static readonly Guid _Id = Guid.Parse("A1056004-3C4D-4E5F-A081-92A3B4C5D6E7");

    public CatalogueNewsOrganisationChannelAliasesLookup()
    {
        Id = _Id;
        ModelType = ModelType.CatalogueNewsOrganisationChannelAliasesLookup;
    }

    /// <summary>Extra channel podcast name → canonical NewsOrganisation name.</summary>
    [JsonPropertyName("organisationNameByChannelPublisherName")]
    [JsonPropertyOrder(10)]
    public Dictionary<string, string> OrganisationNameByChannelPublisherName { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(CatalogueNewsOrganisationChannelAliasesLookup);

    public static CatalogueNewsOrganisationChannelAliasesLookup CreateFromSeed() => new()
    {
        OrganisationNameByChannelPublisherName = new Dictionary<string, string>(
            CatalogueNewsOrganisationChannelAliases.OrganisationNameByChannelPublisherName,
            StringComparer.OrdinalIgnoreCase)
    };
}
