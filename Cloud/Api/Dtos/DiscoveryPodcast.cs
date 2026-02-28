using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoveryPodcast
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("visible")]
    public required bool IsVisible { get; set; }

    [JsonPropertyName("visibleEpisodes")]
    public required int VisibleEpisodes { get; set; }
}