using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.News;

[CosmosSelector(ModelType.NewsOrganisation)]
public sealed class NewsOrganisation : Publisher, IHasYouTubeIndexingSettings
{
    public NewsOrganisation()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.NewsOrganisation;
    }

    /// <summary>
    /// Sets <see cref="Name"/> and a provisional, never-empty file key built from this constructor's own new
    /// <c>Id</c>. Setting <c>Id</c> in an object initializer afterwards does not update the key, and the key is not
    /// checked for uniqueness: code that saves a new item must set <c>FileKey</c> from <c>IFileKeyAllocator</c>.
    /// </summary>
    public NewsOrganisation(string name) : this()
    {
        Name = name;
        FileKey = FileKeyFactory.GetNewsOrganisationFileKey(name, Id);
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

    /// <summary>The YouTube discovery settings and hints live on <see cref="Indexing"/>.</summary>
    [JsonIgnore]
    public IYouTubeIndexingSettings? YouTubeIndexing => Indexing;
}
