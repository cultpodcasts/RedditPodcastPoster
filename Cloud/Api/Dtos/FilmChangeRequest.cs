using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Services;

namespace Api.Dtos;

/// <summary>
/// PATCH body for <c>film/{id}</c>. A film is a publisher that is also playable, so this is the IMDb canonical id,
/// the shared publisher fields (publisher, description, removed, search terms, hashtag and language live on the publisher) and the playable
/// fields a film has. There is no title (the display name is <c>name</c>, which is changed by <c>POST film/name/{identifier}</c>).
/// Omitted (null) = unchanged.
/// </summary>
public class FilmChangeRequest : PublisherFieldsChangeRequest
{
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    /// <summary>IMDb title id <c>tt</c> + 7 to 10 digits. <c>""</c> clears; anything else is a 400.</summary>
    [JsonPropertyName("imdbId")]
    public string? ImdbId { get; set; }

    /// <summary>TMDB movie id. <c>0</c> clears; negative is a 400.</summary>
    [JsonPropertyName("tmdbId")]
    public int? TmdbId { get; set; }

    /// <summary>
    /// Per-key link patch, as the episode edit API: a blank <c>url</c> and <c>image</c> removes the key; otherwise the
    /// key is resolved from the URL (so a YouTube URL lands under <c>youtube</c>). A URL must be absolute http(s).
    /// </summary>
    [JsonPropertyName("services")]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    /// <summary>
    /// The trailers to keep, in display order, each with an optional new title. Trailers left out are removed;
    /// [] removes all. An id the film does not have is a 400. Trailers are added by POST episode/.../trailer.
    /// Send <see cref="ExpectedTrailerIds"/> with this field. A stored trailer whose id is not in that list is a 409.
    /// </summary>
    [JsonPropertyName("trailers")]
    public FilmTrailerChangeRequest[]? Trailers { get; set; }

    /// <summary>
    /// Trailer ids the client last saw. When <see cref="Trailers"/> is sent, a stored trailer whose id is not in
    /// this list is a 409 and nothing is saved. Omitted or empty means the client listed no trailer ids.
    /// </summary>
    [JsonPropertyName("expectedTrailerIds")]
    public Guid[]? ExpectedTrailerIds { get; set; }

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
