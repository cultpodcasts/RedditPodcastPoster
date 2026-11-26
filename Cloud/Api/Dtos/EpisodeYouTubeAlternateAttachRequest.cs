using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Body for <c>POST episode/{podcastId}/{episodeId}/youtube-alternate</c> (RPP #1411).
/// <c>targetKind</c> is <c>Film</c>, <c>NewsReport</c>, <c>TvShowEpisode</c> or <c>Episode</c>.
/// </summary>
public sealed class EpisodeYouTubeAlternateAttachRequest
{
    [JsonPropertyName("targetKind")]
    public string? TargetKind { get; set; }

    [JsonPropertyName("targetId")]
    public Guid TargetId { get; set; }
}
