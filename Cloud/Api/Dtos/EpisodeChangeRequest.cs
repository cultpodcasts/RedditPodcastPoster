using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace Api.Dtos;

public class EpisodeChangeRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("posted")]
    public bool? Posted { get; set; }

    [JsonPropertyName("tweeted")]
    public bool? Tweeted { get; set; }

    [JsonPropertyName("bluesky")]
    public bool? BlueskyPosted { get; set; }

    [JsonPropertyName("ignored")]
    public bool? Ignored { get; set; }

    [JsonPropertyName("removed")]
    public bool? Removed { get; set; }

    [JsonPropertyName("explicit")]
    public bool? Explicit { get; set; }

    [JsonPropertyName("release")]
    public DateTime? Release { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("urls")] 
    public ServiceUrls? Urls { get; set; }

    [JsonPropertyName("images")] 
    public ServiceImageUrls? Images { get; set; }

    [JsonPropertyName("subjects")]
    public string[]? Subjects { get; set; }

    [JsonPropertyName("searchTerms")] 
    public string? SearchTerms { get; set; }

    [JsonPropertyName("lang")] 
    public string? Language { get; set; }

    [JsonIgnore]
    public bool HasChange =>
        Title != null ||
        Description != null ||
        Posted != null ||
        Tweeted != null ||
        BlueskyPosted != null ||
        Ignored != null ||
        Removed != null ||
        Explicit != null ||
        Release != null ||
        Duration != null ||
        Urls != null ||
        Images != null ||
        Subjects != null ||
        SearchTerms != null ||
        Language != null;
}