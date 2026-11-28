using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>One trailer to keep in a TV show PATCH <c>trailers</c> list: its id and an optional new title.</summary>
public sealed class TvShowTrailerChangeRequest
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>Null or blank keeps the current title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

