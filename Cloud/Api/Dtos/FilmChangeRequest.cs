using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// PATCH body for <c>film/{id}</c>. A film is a publisher that is also playable, so this is the IMDb canonical id,
/// the shared publisher fields (publisher, description, removed, search terms, hashtag and language live on the publisher) and the playable
/// fields a film has. There is no title (the display name is <c>name</c>, which is not editable here).
/// Omitted (null) = unchanged.
/// </summary>
public class FilmChangeRequest : PublisherFieldsChangeRequest
{
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    /// <summary>A year <c>yyyy</c> or a calendar date <c>yyyy-MM-dd</c>. Anything else is a 400.</summary>
    [JsonPropertyName("release")]
    public string? Release { get; set; }

    /// <summary>TimeSpan, e.g. <c>1:42:00</c>. Invalid or negative is a 400.</summary>
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("explicit")]
    public bool? Explicit { get; set; }

    [JsonPropertyName("ignored")]
    public bool? Ignored { get; set; }

    /// <summary>Replaces the curator subjects; [] clears. Dropped subjects are remembered as removed.</summary>
    [JsonPropertyName("subjects")]
    public string[]? Subjects { get; set; }

    /// <summary>Same rule as episodes: replaces; [] clears (stored as null); blanks dropped; names trimmed.</summary>
    [JsonPropertyName("guests")]
    public string[]? Guests { get; set; }

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
}
