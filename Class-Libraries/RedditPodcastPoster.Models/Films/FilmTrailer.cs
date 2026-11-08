using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.Films;

/// <summary>
/// A short video that promotes a <see cref="Film"/> (a trailer, teaser or clip). It lives on the film document only:
/// it has no search document and no catalogue page of its own, and the film page lists it.
/// <see cref="Id"/> is the id of the podcast episode the trailer came from ("Attach as trailer"), so a re-sent attach
/// is recognised and the episode's old short link can be pointed at the film.
/// </summary>
public sealed class FilmTrailer
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(2)]
    public string Title { get; set; } = string.Empty;

    /// <summary>The page that plays the trailer (YouTube when the source episode had it).</summary>
    [JsonPropertyName("url")]
    [JsonPropertyOrder(3)]
    public required Uri Url { get; set; }

    [JsonPropertyName("image")]
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Image { get; set; }

    /// <summary>When the trailer was published (UTC), from the source episode.</summary>
    [JsonPropertyName("release")]
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(6)]
    public TimeSpan Length { get; set; }

    /// <summary>
    /// Source episode Spotify / Apple / YouTube ids. Optional and omitted when empty so existing trailers stay
    /// wire-compatible. Used so a Spotify- or Apple-only re-find still matches this trailer.
    /// </summary>
    [JsonPropertyName("ids")]
    [JsonPropertyOrder(7)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EpisodeIds? Ids { get; set; }

    /// <summary>
    /// Source episode YouTube / Spotify / Apple service URLs (url only). Optional and omitted when empty.
    /// <see cref="Url"/> remains the playback page; these let paste and indexer match every platform the episode had.
    /// </summary>
    [JsonPropertyName("services")]
    [JsonPropertyOrder(8)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, ServiceLink>? Services { get; set; }
}
