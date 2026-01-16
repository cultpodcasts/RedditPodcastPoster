using System.Text.Json.Serialization;

namespace Api.Dtos;

public class PodcastRenameResponse
{
    [JsonPropertyName("indexState")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SearchIndexerState IndexState { get; set; }
}