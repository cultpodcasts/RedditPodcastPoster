using System.Text.Json.Serialization;
using Api.Contracts;

namespace Api.Dtos;

/// <summary>Command outcome for POST episode/{podcastId}/{episodeId}/tvshow-trailer. The client GETs the TV show for its read model.</summary>
[ExportContract]
public sealed class EpisodeTvShowTrailerAttachResponse
{
    [JsonPropertyName("tvShowId")]
    public Guid? TvShowId { get; init; }

    [JsonPropertyName("tvShowName")]
    public string? TvShowName { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("failureIndexing")]
    public bool FailureIndexing { get; init; }

    /// <summary>True when the request had <c>createShow</c> and this call created the show.</summary>
    [JsonPropertyName("showCreated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ShowCreated { get; init; }
}

