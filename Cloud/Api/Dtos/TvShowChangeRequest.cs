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

    /// <summary>
    /// The trailers to keep, in display order, each with an optional new title. Trailers left out are removed;
    /// [] removes all. An id the show does not have is a 400. Trailers are added by POST
    /// episode/.../tvshow-trailer. Send <see cref="ExpectedTrailerIds"/> with this field. A stored trailer whose id is
    /// not in that list is a 409.
    /// </summary>
    [JsonPropertyName("trailers")]
    public TvShowTrailerChangeRequest[]? Trailers { get; set; }

    /// <summary>
    /// Trailer ids the client last saw. When <see cref="Trailers"/> is sent, a stored trailer whose id is not in this
    /// list is a 409 and nothing is saved. Omitted or empty means the client listed no trailer ids.
    /// </summary>
    [JsonPropertyName("expectedTrailerIds")]
    public Guid[]? ExpectedTrailerIds { get; set; }
}
