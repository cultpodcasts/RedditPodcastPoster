using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace CultPodcasts.DatabasePublisher.PublicModels;

/// <summary>A film is its own file: it has no parent, so there is no nested list. <c>release</c> and <c>urls</c> follow <see cref="PublicPlayable"/>.</summary>
public sealed class PublicFilm(Guid id) : PublicCatalogueEntity(id, ModelType.Film)
{
    [JsonPropertyName("name")]
    [JsonPropertyOrder(3)]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    [JsonPropertyOrder(4)]
    public string? Description { get; init; }

    [JsonPropertyName("release")]
    [JsonPropertyOrder(7)]
    public string? Release { get; init; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(8)]
    public TimeSpan Length { get; init; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(9)]
    public bool Explicit { get; init; }

    [JsonPropertyName("imdbId")]
    [JsonPropertyOrder(10)]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tmdbId")]
    [JsonPropertyOrder(11)]
    public int? TmdbId { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(13)]
    public List<string>? Subjects { get; set; }

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(14)]
    public SortedDictionary<string, Uri> Urls { get; set; } = new(StringComparer.Ordinal);
}
