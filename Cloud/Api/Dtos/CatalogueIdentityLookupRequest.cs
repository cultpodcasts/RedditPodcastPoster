using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Best-effort IMDb / TMDB / TheTVDB lookup for the Identity tab (edit-time).
/// Does not write Cosmos — the curator applies returned ids to the form, then saves.
/// </summary>
public class CatalogueIdentityLookupRequest
{
    /// <summary><c>Film</c>, <c>TvShow</c>, or <c>TvShowEpisode</c>.</summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; set; }

    /// <summary>Film / TV show name, or episode title.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    /// <summary>
    /// Optional film year from the release field. Narrows ambiguous TMDB hits;
    /// when exactly one hit matches that year, identity resolves without a curator pick (same as create-time).
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>Parent show name when <see cref="Kind"/> is <c>TvShowEpisode</c>.</summary>
    [JsonPropertyName("showName")]
    public string? ShowName { get; set; }

    /// <summary>Optional known parent TMDB id (edit episode when the show already has identity).</summary>
    [JsonPropertyName("showTmdbId")]
    public int? ShowTmdbId { get; set; }

    /// <summary>Optional known parent TheTVDB id.</summary>
    [JsonPropertyName("showTvdbId")]
    public long? ShowTvdbId { get; set; }

    /// <summary>Optional known parent IMDb title id.</summary>
    [JsonPropertyName("showImdbId")]
    public string? ShowImdbId { get; set; }

    /// <summary>
    /// Curator answer after an ambiguous prompt: <c>select</c> with <see cref="SelectedIdentityKey"/>;
    /// omitted / unset on the first probe.
    /// </summary>
    [JsonPropertyName("identityDecision")]
    public string? IdentityDecision { get; set; }

    /// <summary>Candidate key from a prior ambiguous response when deciding <c>select</c>.</summary>
    [JsonPropertyName("selectedIdentityKey")]
    public string? SelectedIdentityKey { get; set; }
}
