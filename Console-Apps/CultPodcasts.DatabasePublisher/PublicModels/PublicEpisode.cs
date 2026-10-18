using System.Text.Json.Serialization;
using CultPodcasts.DatabasePublisher.Mapping;

namespace CultPodcasts.DatabasePublisher.PublicModels;

public class PublicEpisode
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

    /// <summary>The release instant: what episodes are ordered by. It is not itself written; see <see cref="ReleaseText"/>.</summary>
    [JsonIgnore]
    public DateTime Release { get; init; }

    private string? _releaseText;

    /// <summary>
    /// The <c>release</c> text. Unless one is given, which is the text of the previous publish when this episode
    /// was already there with the same instant (see <see cref="BaselineReleaseText"/>), it is the instant as the
    /// serializer has always written it.
    /// </summary>
    [JsonPropertyName("release")]
    [JsonPropertyOrder(7)]
    public string ReleaseText
    {
        get => _releaseText ?? PublicRelease.FormatInstant(Release);
        init => _releaseText = value;
    }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(8)]
    public TimeSpan Length { get; init; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(9)]
    public bool Explicit { get; init; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(10)]
    public string? SpotifyId { get; set; }

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(11)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeId")]
    [JsonPropertyOrder(12)]
    public string? YouTubeId { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(13)]
    public List<string>? Subjects { get; set; }

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(14)]
    public PublicServiceUrls Urls { get; set; } = new();
}