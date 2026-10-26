using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Services;

namespace Api.Dtos;

/// <summary>
/// Playable fields a curator can PATCH on a news report or TV-show episode. Omitted (null) = unchanged.
/// Validated before anything is applied, so a 400 leaves the playable untouched.
/// </summary>
public abstract class PlayableFieldsChangeRequest
{
    /// <summary>Blank is ignored (a title is required).</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Calendar date <c>yyyy-MM-dd</c>. Anything else is a 400.</summary>
    [JsonPropertyName("release")]
    public string? Release { get; set; }

    /// <summary>TimeSpan, e.g. <c>0:06:30</c>. Invalid or negative is a 400.</summary>
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    /// <summary>Same as the episode edit API: sets the Reddit posted flag.</summary>
    [JsonPropertyName("posted")]
    public bool? Posted { get; set; }

    /// <summary>Same as the episode edit API: sets the flag; true → false also deletes the tweet.</summary>
    [JsonPropertyName("tweeted")]
    public bool? Tweeted { get; set; }

    /// <summary>
    /// Same as the episode edit API: true deletes the Bluesky post, and clears the Bluesky state only when the post is
    /// gone. Bluesky posted cannot be set to true here.
    /// </summary>
    [JsonPropertyName("unBluesky")]
    public bool? UnBluesky { get; set; }

    [JsonPropertyName("explicit")]
    public bool? Explicit { get; set; }

    [JsonPropertyName("ignored")]
    public bool? Ignored { get; set; }

    /// <summary>true also deletes the playable's search document.</summary>
    [JsonPropertyName("removed")]
    public bool? Removed { get; set; }

    /// <summary>Replaces the curator subjects; [] clears. Dropped subjects are remembered as removed.</summary>
    [JsonPropertyName("subjects")]
    public string[]? Subjects { get; set; }

    [JsonPropertyName("searchTerms")]
    public string? SearchTerms { get; set; }

    /// <summary>"" clears.</summary>
    [JsonPropertyName("hashTag")]
    public string? HashTag { get; set; }

    /// <summary>
    /// Guest person names; replaces the list. [] clears (stored as null). Blank names are dropped and
    /// names trimmed. Same rule as the episode edit API.
    /// </summary>
    [JsonPropertyName("guests")]
    public string[]? Guests { get; set; }

    /// <summary>"" or English clears (stored as null = English).</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    /// Per-key link patch, as film / episode: blank <c>url</c> and <c>image</c> removes the key; otherwise the
    /// key is resolved from the URL. Absolute http(s) only.
    /// </summary>
    [JsonPropertyName("services")]
    public Dictionary<string, ServiceLink>? Services { get; set; }
}
