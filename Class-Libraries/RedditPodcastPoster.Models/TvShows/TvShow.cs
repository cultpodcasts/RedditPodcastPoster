using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.TvShows;

public class TvShow
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(20)]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(21)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(22)]
    public string? Language { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(25)]
    public bool? Removed { get; set; }

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(80)]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }
}
