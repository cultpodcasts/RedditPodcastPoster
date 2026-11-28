using System.Text.Json.Serialization;
using Api.Contracts;

namespace Api.Dtos;

/// <summary>
/// Body of POST episode/{podcastId}/{episodeId}/tvshow-trailer: <c>tvShowId</c>, or <c>createShow</c> with
/// <c>tvShowName</c> (and optional <c>publisher</c>), plus an optional trailer <c>title</c>.
/// </summary>
[ExportContract]
public class EpisodeTvShowTrailerAttachRequest
{
    [JsonPropertyName("tvShowId")]
    public Guid? TvShowId { get; set; }

    [JsonPropertyName("tvShowName")]
    public string? TvShowName { get; set; }

    [JsonPropertyName("createShow")]
    public bool? CreateShow { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

