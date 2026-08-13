using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;

namespace Api.Models;

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

    /// <summary>
    /// When true, clear Bluesky post state and delete the remote post.
    /// Bluesky posted state is not settable via episode change — only via publish/indexer.
    /// </summary>
    [JsonPropertyName("unBluesky")]
    public bool? UnBluesky { get; set; }

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

    [JsonPropertyName("hashTag")]
    public string? HashTag { get; set; }

    [JsonPropertyName("lang")]
    public string? Language { get; set; }

    [JsonPropertyName("guests")]
    public string[]? Guests { get; set; }

    [JsonIgnore]
    public bool HasChange =>
        Title != null ||
        Description != null ||
        Posted != null ||
        Tweeted != null ||
        UnBluesky != null ||
        Ignored != null ||
        Removed != null ||
        Explicit != null ||
        Release != null ||
        Duration != null ||
        Urls != null ||
        Images != null ||
        Subjects != null ||
        SearchTerms != null ||
        HashTag != null ||
        Language != null ||
        Guests != null;
}