using System.Text.Json.Serialization;
using Api.Contracts;

namespace Api.Dtos;

/// <summary>
/// Body of POST episode/{podcastId}/{episodeId}/trailer: <c>filmId</c>, or <c>createFilm</c> with <c>filmName</c>,
/// plus an optional trailer <c>title</c>. Every key is optional on the wire (omit, do not send null), so each property
/// carries <see cref="JsonIgnoreCondition.WhenWritingNull"/> to make the generated Zod key <c>.optional()</c>.
/// </summary>
[ExportContract]
public class EpisodeTrailerAttachRequest
{
    [JsonPropertyName("filmId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? FilmId { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    /// <summary>With <see cref="FilmName"/> instead of <see cref="FilmId"/>: create the film when no film has the name.</summary>
    [JsonPropertyName("createFilm")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CreateFilm { get; set; }

    [JsonPropertyName("filmName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilmName { get; set; }

    /// <summary>Answer to an ambiguous-identity prompt for a new film: <c>select</c>, <c>continue</c>, or <c>cancel</c>.</summary>
    [JsonPropertyName("identityDecision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdentityDecision { get; set; }

    /// <summary>Candidate <c>key</c> when <see cref="IdentityDecision"/> is <c>select</c> (e.g. <c>tmdb:123</c>).</summary>
    [JsonPropertyName("selectedIdentityKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SelectedIdentityKey { get; set; }
}
