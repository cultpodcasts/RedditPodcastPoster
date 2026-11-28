using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Curator GET for a TV show: the shared publisher fields plus canonical ids.</summary>
public class TvShowDto : ServiceIdsDto
{
    [JsonPropertyName("imdbId")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tvdbId")]
    public long? TvdbId { get; set; }

    [JsonPropertyName("tmdbId")]
    public int? TmdbId { get; set; }

    /// <summary>Trailers attached from podcast episodes, in display order. Omitted when there are none.</summary>
    [JsonPropertyName("trailers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TvShowTrailerDto[]? Trailers { get; set; }
}
