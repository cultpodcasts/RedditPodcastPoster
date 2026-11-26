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
    /// Same as the episode edit API: true sets the legacy Bluesky posted flag (curator/manual mark); true → false
    /// deletes the Bluesky post and clears state only when the post is gone.
    /// </summary>
    [JsonPropertyName("blueskyPosted")]
    public bool? BlueskyPosted { get; set; }

    /// <summary>
    /// Backward-compat alias for <c>blueskyPosted: false</c>: true deletes the Bluesky post and clears state only
    /// when the post is gone.
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
    /// key is resolved from the URL. Absolute http(s) only. Cloudflare Worker OpenAPI documents the same field on
    /// news report and TV episode PATCH via <c>playableFieldsChangeShape.services</c> (Api #193 / cultpodcasts-api 2.10.4+).
    /// </summary>
    [JsonPropertyName("services")]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    /// <summary>
    /// Additional YouTube uploads to keep, in display order (RPP #1526). Existing entries are identified by
    /// <c>youTubeId</c>; a new entry is added from a YouTube <c>url</c>. Entries left out are removed; [] removes
    /// all. Send <see cref="ExpectedAdditionalYouTubeIds"/> with this field. A stored id not in that list is a 409.
    /// </summary>
    [JsonPropertyName("additionalYouTube")]
    public YouTubeAlternateChangeRequest[]? AdditionalYouTube { get; set; }

    /// <summary>
    /// Additional YouTube ids the client last saw. When <see cref="AdditionalYouTube"/> is sent, a stored id that is
    /// not in this list is a 409 and nothing is saved.
    /// </summary>
    [JsonPropertyName("expectedAdditionalYouTubeIds")]
    public string[]? ExpectedAdditionalYouTubeIds { get; set; }

    /// <summary>
    /// The YouTube id of an additional upload to swap into the primary <c>services.youtube</c> (RPP #1526). The
    /// previous primary becomes an additional upload. The id must be in <see cref="AdditionalYouTube"/> when that
    /// field is sent, otherwise it must already be on the item. Unknown id is a 400.
    /// </summary>
    [JsonPropertyName("promoteYouTubeId")]
    public string? PromoteYouTubeId { get; set; }
}
