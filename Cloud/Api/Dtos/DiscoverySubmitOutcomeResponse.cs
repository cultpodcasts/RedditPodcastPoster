using System.Text.Json.Serialization;
using Api.Models;

namespace Api.Dtos;

public class DiscoverySubmitOutcomeResponse
{
    [JsonPropertyName("results")]
    public DiscoverySubmitOutcomeItemDto[] Results { get; set; } = [];
}

public class DiscoverySubmitOutcomeItemDto
{
    [JsonPropertyName("discoveryItemId")]
    public Guid DiscoveryItemId { get; set; }

    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiscoverySubmitOutcomeState State { get; set; } = DiscoverySubmitOutcomeState.Pending;

    [JsonPropertyName("podcastId")]
    public Guid? PodcastId { get; set; }

    [JsonPropertyName("episodeId")]
    public Guid? EpisodeId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("episodeName")]
    public string? EpisodeName { get; set; }

    [JsonPropertyName("showName")]
    public string? ShowName { get; set; }
}
