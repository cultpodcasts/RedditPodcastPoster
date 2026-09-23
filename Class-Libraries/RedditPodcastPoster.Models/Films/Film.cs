using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.Films;

/// <summary>
/// Standalone made-as-film playable (no parent). ADR-0002 / epic S-001…S-002.
/// Film identity is not the podcast <c>ids</c> bag: no Spotify/Apple; YouTube is first-class when present.
/// Streaming destinations (Netflix, etc.) use <see cref="Services"/>.
/// </summary>
public class Film
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(10)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(20)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("release")]
    [JsonPropertyOrder(30)]
    public DateTime Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(31)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(32)]
    public bool Explicit { get; set; }

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(45)]
    public string? Language { get; set; }

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(43)]
    public bool Ignored { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(44)]
    public bool Removed { get; set; }

    /// <summary>
    /// YouTube video id when the film is (also) on YouTube. Never Spotify/Apple — those are podcast-episode identities.
    /// </summary>
    [JsonPropertyName("youtubeId")]
    [JsonPropertyOrder(53)]
    public string? YouTubeId { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(70)]
    public List<string> Subjects { get; set; } = [];

    [JsonPropertyName("removedSubjects")]
    [JsonPropertyOrder(71)]
    public List<string> RemovedSubjects { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(80)]
    public string? SearchTerms { get; set; }

    /// <summary>
    /// Non-YouTube streaming links (url/image per catalog key). Value type reused from Episode for JSON shape only.
    /// </summary>
    [JsonPropertyName("services")]
    [JsonPropertyOrder(151)]
    public Dictionary<string, EpisodeServiceLink>? Services { get; set; }

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }
}
