using System.Text.Json.Serialization;

namespace Api.Dtos;

public class SubmitEpisodeDetails(
    bool spotify,
    bool apple,
    bool youTube,
    bool BBC,
    bool internetArchive,
    string[] subjects,
    PersonMatchDto[] people,
    PersonMatchDto[] guestSuggestions
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

    [JsonPropertyName("people")]
    public PersonMatchDto[] People { get; private set; } = people;

    [JsonPropertyName("bbc")]
    public bool BBC { get; private set; } = BBC;

    [JsonPropertyName("internetArchive")]
    public bool InternetArchive { get; private set; } = internetArchive;

    [JsonPropertyName("guestSuggestions")]
    public PersonMatchDto[] GuestSuggestions { get; private set; } = guestSuggestions;
}