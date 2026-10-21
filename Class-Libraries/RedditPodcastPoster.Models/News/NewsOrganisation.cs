using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.News;

[CosmosSelector(ModelType.NewsOrganisation)]
public sealed class NewsOrganisation : Publisher
{
    public NewsOrganisation()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.NewsOrganisation;
    }

    public NewsOrganisation(string name) : this()
    {
        Name = name;
        FileKey = FileKeyFactory.GetNewsOrganisationFileKey(name);
    }

    /// <summary>
    /// Episode-discovery settings kept from the migrated podcast (JSON <c>indexing</c>). When
    /// <see cref="CatalogueIndexingSource.IsAutoIndexed"/>, the hourly indexer finds new playables.
    /// Omitted when unset.
    /// </summary>
    [JsonPropertyName("indexing")]
    [JsonPropertyOrder(300)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CatalogueIndexingSource? Indexing { get; set; }
}
