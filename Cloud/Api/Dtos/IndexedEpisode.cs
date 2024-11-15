using System.Text.Json.Serialization;

namespace Api.Dtos;

public class IndexedEpisode
{
    [JsonPropertyName("episodeId")]
    public required Guid EpisodeId { get; set; }

    [JsonPropertyName("spotify")]
    public required bool Spotify { get; set; }

    [JsonPropertyName("apple")]
    public required bool Apple { get; set; }

    [JsonPropertyName("youtube")]
    public required bool YouTube { get; set; }

    [JsonPropertyName("subjects")]
    public required string[] Subjects { get; set; }
}