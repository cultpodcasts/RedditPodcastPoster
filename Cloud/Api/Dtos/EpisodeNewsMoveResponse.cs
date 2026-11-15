using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Command outcome for POST episode/{podcastId}/{episodeId}/news and POST podcast/{podcastId}/news. The client GETs
/// the news organisation and its reports for the read model.
/// </summary>
public sealed class EpisodeNewsMoveResponse
{
    [JsonPropertyName("newsOrganisationId")]
    public Guid? NewsOrganisationId { get; init; }

    [JsonPropertyName("newsOrganisationName")]
    public string? NewsOrganisationName { get; init; }

    /// <summary>True when the move created the news organisation.</summary>
    [JsonPropertyName("createdOrganisation")]
    public bool CreatedOrganisation { get; init; }

    /// <summary>The single moved episode id (the news report keeps it). Only on the single-episode route.</summary>
    [JsonPropertyName("episodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? EpisodeId { get; init; }

    /// <summary>Episode ids moved by this request (each is now a news report with the same id).</summary>
    [JsonPropertyName("movedEpisodeIds")]
    public Guid[] MovedEpisodeIds { get; init; } = [];

    /// <summary>Episode ids an earlier request already moved to this organisation (a re-send).</summary>
    [JsonPropertyName("alreadyMovedEpisodeIds")]
    public Guid[] AlreadyMovedEpisodeIds { get; init; } = [];

    /// <summary>Requested ids that are not episodes of the podcast. Any of these refuses the whole request.</summary>
    [JsonPropertyName("notFoundEpisodeIds")]
    public Guid[] NotFoundEpisodeIds { get; init; } = [];

    /// <summary>
    /// Requested ids that are already a TV episode, a film, or a news report of another organisation. Any of these
    /// refuses the whole request.
    /// </summary>
    [JsonPropertyName("conflictEpisodeIds")]
    public Guid[] ConflictEpisodeIds { get; init; } = [];

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("failureIndexing")]
    public bool FailureIndexing { get; init; }
}
