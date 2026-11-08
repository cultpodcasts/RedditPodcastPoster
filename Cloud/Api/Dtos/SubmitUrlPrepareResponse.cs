using System.Text.Json.Serialization;

namespace Api.Dtos;

public class SubmitUrlPrepareResponse
{
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("podcastName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PodcastName { get; init; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? Duration { get; init; }

    [JsonPropertyName("release")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Release { get; init; }

    [JsonPropertyName("image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Image { get; init; }

    [JsonPropertyName("explicit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Explicit { get; init; }

    [JsonPropertyName("publisher")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Publisher { get; init; }

    [JsonPropertyName("showName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShowName { get; init; }

    /// <summary>
    /// True when the page describes a standalone film (e.g. JSON-LD <c>Movie</c>). The Worker caches it in
    /// StreamMeta KV and returns it in <c>prefetchedMeta.madeAsFilm</c> on submit, so the classifier files
    /// the URL as a Film rather than a podcast episode. Always written so a missing key means an old Worker.
    /// </summary>
    [JsonPropertyName("madeAsFilm")]
    public bool MadeAsFilm { get; init; }
}
