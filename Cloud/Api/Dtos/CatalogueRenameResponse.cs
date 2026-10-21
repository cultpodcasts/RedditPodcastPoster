using System.Text.Json.Serialization;
using Api.Dtos.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Models;

namespace Api.Dtos;

/// <summary>Same shape as <see cref="PodcastRenameResponse"/>: the state of the immediate search update.</summary>
public class CatalogueRenameResponse
{
    [JsonPropertyName("indexState")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SearchIndexerState IndexState { get; set; }

    public static CatalogueRenameResponse From(EntitySearchIndexerResponse indexed) =>
        new() { IndexState = indexed.ToDto() };
}
