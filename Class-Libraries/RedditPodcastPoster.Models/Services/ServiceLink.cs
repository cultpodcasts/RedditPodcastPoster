using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Services;

/// <summary>
/// Streaming destination link (url + optional image) for catalogue playables.
/// Kind-neutral replacement for podcast <c>EpisodeServiceLink</c> on Film / TV / News.
/// </summary>
public class ServiceLink
{
    [JsonPropertyName("url")]
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Url { get; set; }

    [JsonPropertyName("image")]
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Image { get; set; }

    /// <summary>
    /// Language of the destination stream: ISO 639-1 two-letter code (e.g. <c>de</c>),
    /// or a three-letter 639-2/639-3 code where no two-letter code exists.
    /// Null = unspecified (do not assume English; this is link metadata, not
    /// <c>Episode.Language</c> semantics).
    /// </summary>
    [JsonPropertyName("lang")]
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Url is null && Image is null;
}
