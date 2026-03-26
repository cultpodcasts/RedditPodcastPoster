using System.Text.Json.Serialization;

namespace Api.Dtos;

public class SubmitUrlSuccessResponse(
    SubmitItemResponse episode,
    SubmitItemResponse podcast,
    SubmitEpisodeDetails? submitEpisodeDetails,
    Guid? episodeId,
    Guid? podcastId)
{
    [JsonPropertyName("episode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubmitItemResponse Episode { get; private set; } = episode;

    [JsonPropertyName("episodeId")]
    public Guid? EpisodeId { get; private set; } = episodeId;

    [JsonPropertyName("podcastId")]
    public Guid? PodcastId { get; private set; } = podcastId;

    [JsonPropertyName("podcast")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubmitItemResponse Podcast { get; private set; } = podcast;

    [JsonPropertyName("episodeDetails")]
    public SubmitEpisodeDetails? EpisodeDetails { get; private set; } = submitEpisodeDetails;
}