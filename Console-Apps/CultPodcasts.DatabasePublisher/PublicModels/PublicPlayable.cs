using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;

namespace CultPodcasts.DatabasePublisher.PublicModels;

/// <summary>
/// A playable inside a TV show or news organisation file. Mirrors <see cref="PublicEpisode"/>, except that
/// <c>release</c> keeps the catalogue precision (year, date or UTC date-time) and <c>urls</c> is keyed by
/// catalogue service key, because these kinds are not limited to Spotify, Apple and YouTube.
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
    public CatalogueRelease? Release { get; init; }

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
