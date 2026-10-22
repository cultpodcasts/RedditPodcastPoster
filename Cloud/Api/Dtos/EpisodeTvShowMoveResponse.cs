using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Command outcome for POST episode/{podcastId}/{episodeId}/tvshow. The client opens
/// <c>/tv/{tvShowName}/{shortId}</c> and GETs the TV episode for its read model.
/// </summary>
public sealed class EpisodeTvShowMoveResponse
{
    [JsonPropertyName("tvShowId")]
    public Guid? TvShowId { get; init; }

    [JsonPropertyName("tvShowName")]
    public string? TvShowName { get; init; }

    /// <summary>The TV episode id (the moved episode keeps its id).</summary>
    [JsonPropertyName("episodeId")]
    public Guid? EpisodeId { get; init; }

    /// <summary>True when the move created the TV show.</summary>
    [JsonPropertyName("createdShow")]
    public bool CreatedShow { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("failureIndexing")]
    public bool FailureIndexing { get; init; }
}
