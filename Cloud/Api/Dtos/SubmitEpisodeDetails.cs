using System.Text.Json.Serialization;

namespace Api.Dtos;

public class SubmitEpisodeDetails(
    bool spotify,
    bool apple,
    bool youTube,
    string[] subjects
)
{
    [JsonPropertyName("spotify")]
    public bool Spotify { get; private set; } = spotify;

    [JsonPropertyName("apple")]
    public bool Apple { get; private set; } = apple;

    [JsonPropertyName("youtube")]
    public bool YouTube { get; private set; } = youTube;

    [JsonPropertyName("subjects")]
    public string[] Subject { get; private set; } = subjects;
}