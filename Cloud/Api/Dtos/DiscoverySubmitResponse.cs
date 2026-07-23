using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoverySubmitResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("errorsOccurred")]
    public bool ErrorsOccurred { get; set; }

    [JsonPropertyName("results")]
    public required Item[] Results { get; set; }

    [JsonPropertyName("searchIndexerState")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required SearchIndexerState SearchIndexerState { get; set; }

    public class Item
    {
        [JsonPropertyName("discoveryItemId")]
        public required Guid DiscoveryItemId { get; set; }

        [JsonPropertyName("podcastId")]
        public Guid? PodcastId { get; set; }

        [JsonPropertyName("episodeId")]
        public Guid? EpisodeId { get; set; }

        [JsonPropertyName("message")]
        public required string Message { get; set; }
    }
}
