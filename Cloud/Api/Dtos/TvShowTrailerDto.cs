using System.Text.Json.Serialization;
using Api.Contracts;

namespace Api.Dtos;

/// <summary>A trailer on <see cref="TvShowDto"/>. <c>id</c> is the id of the episode it was attached from.</summary>
[ExportContract]
public sealed class TvShowTrailerDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public Uri? Url { get; set; }

    [JsonPropertyName("image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Image { get; set; }

    [JsonPropertyName("release")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Release { get; set; }

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;
}
