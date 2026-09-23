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

    [JsonIgnore]
    public bool IsEmpty => Url is null && Image is null;
}
