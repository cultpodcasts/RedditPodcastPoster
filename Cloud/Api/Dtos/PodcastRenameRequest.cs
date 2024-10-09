using System.Text.Json.Serialization;

namespace Api.Dtos;

public class PodcastRenameRequest
{
    [JsonPropertyName("newPodcastName")]
    public required string NewPodcastName { get; set; }
}