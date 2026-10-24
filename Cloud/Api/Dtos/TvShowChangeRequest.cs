using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>PATCH body for <c>tvshow/{id}</c>: identity ids plus the shared publisher fields and service ids.</summary>
public class TvShowChangeRequest : ServiceIdsChangeRequest
{
    /// <summary>IMDb title id <c>tt</c> + 7 to 10 digits. <c>""</c> clears; anything else is a 400.</summary>
    [JsonPropertyName("imdbId")]
    public string? ImdbId { get; set; }

    /// <summary>TheTVDB series id. <c>0</c> clears; negative is a 400.</summary>
    [JsonPropertyName("tvdbId")]
    public long? TvdbId { get; set; }

    /// <summary>TMDB series id. <c>0</c> clears; negative is a 400.</summary>
    [JsonPropertyName("tmdbId")]
    public int? TmdbId { get; set; }
}
