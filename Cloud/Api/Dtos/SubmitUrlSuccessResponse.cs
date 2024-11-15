using System.Text.Json.Serialization;

namespace Api.Dtos;

public class SubmitUrlSuccessResponse(
    SubmitItemResponse episode,
    SubmitItemResponse podcast,
    SubmitEpisodeDetails? submitEpisodeDetails,
    Guid? episodeId)
{
    [JsonPropertyName("episode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubmitItemResponse Episode { get; private set; } = episode;

    [JsonPropertyName("episodeId")]
    public Guid? EpisodeId { get; private set; } = episodeId;

    [JsonPropertyName("podcast")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubmitItemResponse Podcast { get; private set; } = podcast;

    [JsonPropertyName("episodeDetails")]
    public SubmitEpisodeDetails? EpisodeDetails { get; private set; } = submitEpisodeDetails;
}