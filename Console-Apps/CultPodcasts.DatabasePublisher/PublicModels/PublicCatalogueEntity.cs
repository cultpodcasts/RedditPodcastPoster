using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace CultPodcasts.DatabasePublisher.PublicModels;

/// <summary>
/// A file in the public database for a kind other than a podcast. Like <see cref="PublicPodcast"/> it is a
/// <see cref="CosmosSelector"/> only so the file writer can name the file from <see cref="FileKey"/>; the
/// Cosmos bookkeeping members (<c>type</c>, <c>fileKey</c>, <c>_ts</c>) are not part of the public JSON.
/// </summary>
public abstract class PublicCatalogueEntity : CosmosSelector
{
    protected PublicCatalogueEntity(Guid id, ModelType modelType)
    {
        Id = id;
        ModelType = modelType;
    }

    [JsonIgnore]
    public override long Timestamp { get; set; }

    [JsonIgnore]
    public override ModelType ModelType { get; set; }

    [JsonIgnore]
    public override string FileKey { get; set; } = "";
}
