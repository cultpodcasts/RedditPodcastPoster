using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace CultPodcasts.DatabasePublisher.PublicModels;

public sealed class PublicNewsOrganisation(Guid id) : PublicCatalogueEntity(id, ModelType.NewsOrganisation)
{
    [JsonPropertyName("name")]
    [JsonPropertyOrder(3)]
    public string? Name { get; set; }

    [JsonPropertyName("reports")]
    [JsonPropertyOrder(11)]
    public List<PublicNewsReport> Reports { get; set; } = new();
}
