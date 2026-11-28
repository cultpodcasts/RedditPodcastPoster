using System.Text.Json.Serialization;
using Api.Contracts;

namespace Api.Dtos;

/// <summary>
/// Body of POST episode/{podcastId}/{episodeId}/tvshow-trailer: <c>tvShowId</c>, or <c>createShow</c> with
/// <c>tvShowName</c> (and optional <c>publisher</c>), plus an optional trailer <c>title</c>. Every key is optional on
/// the wire, so each property carries <see cref="JsonIgnoreCondition.WhenWritingNull"/> to make the generated Zod key
/// <c>.optional()</c>.
/// </summary>
[ExportContract]
public class EpisodeTvShowTrailerAttachRequest
{
    [JsonPropertyName("tvShowId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? TvShowId { get; set; }

    [JsonPropertyName("tvShowName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TvShowName { get; set; }

    [JsonPropertyName("createShow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CreateShow { get; set; }

    [JsonPropertyName("publisher")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Publisher { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }
}
