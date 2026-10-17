using System.Text.Json.Serialization;

namespace CultPodcasts.DatabasePublisher.PublicModels;

/// <summary>
/// A playable inside a TV show or news organisation file. Mirrors <see cref="PublicEpisode"/>, except that
/// <c>release</c> is always a string that keeps the catalogue precision (<c>2019</c>, <c>2019-05-01</c> or
/// <c>2019-05-01T12:00:00Z</c>, never a number) and <c>urls</c> is a map keyed by catalogue service key
/// (<c>spotify</c>, <c>apple</c>, <c>youtube</c> and the streaming keys such as <c>bbcIplayer</c>,
/// <c>bbcSounds</c>, <c>internetArchive</c>, <c>netflix</c>), because these kinds are not limited to Spotify,
/// Apple and YouTube and do not merge the BBC services into one key.
/// </summary>
public class PublicPlayable
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(3)]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    [JsonPropertyOrder(4)]
    public string? Description { get; init; }

    [JsonPropertyName("release")]
    [JsonPropertyOrder(7)]
    public string? Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(8)]
    public TimeSpan Length { get; init; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(9)]
    public bool Explicit { get; init; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(13)]
    public List<string>? Subjects { get; set; }

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(14)]
    public SortedDictionary<string, Uri> Urls { get; set; } = new(StringComparer.Ordinal);
}
