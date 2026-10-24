using System.Text.Json.Serialization;

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

    /// <summary>Accepted | AcceptError | Rejected | Pending | Unknown</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = "Pending";

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
