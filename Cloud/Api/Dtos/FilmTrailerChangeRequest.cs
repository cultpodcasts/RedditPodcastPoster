using System.Text.Json.Serialization;
using Api.Contracts;

namespace Api.Dtos;

/// <summary>One trailer to keep in a film PATCH <c>trailers</c> list: its id and an optional new title.</summary>
[ExportContract]
public sealed class FilmTrailerChangeRequest
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>Null or blank keeps the current title.</summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }
}
