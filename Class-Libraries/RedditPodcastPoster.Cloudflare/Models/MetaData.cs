using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Cloudflare.Models;

public class MetaData
{
    [JsonPropertyName("episodeTitle")]
    public required string EpisodeTitle { get; set; }

    [JsonPropertyName("releaseDate")]
    public required DateOnly ReleaseDate { get; set; }

    [JsonPropertyName("duration")]
    public required TimeSpan Duration { get; set; }

    /// <summary>
    /// Search-index image encoding (y{q} / s{id} / a{n}{path} / full URL).
    /// Only set on new shortener KV writes from now on; existing records are left unchanged.
    /// </summary>
    [JsonPropertyName("image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Image { get; set; }

    /// <summary>Required to expand YouTube <c>y{q}</c> tokens for og:image.</summary>
    [JsonPropertyName("youtubeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? YoutubeId { get; set; }

    /// <summary>One of the enumeration values that specifies twitter:card selection.</summary>
    [JsonPropertyName("imageAspect")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ShareImageAspect? ImageAspect { get; set; }
}
