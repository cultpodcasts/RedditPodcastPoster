using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// One of a news organisation's additional YouTube channels (#1370) in the curator API: the channel id and its optional
/// playlist on that channel (null on GET when the channel is claimed whole; null or blank on PATCH for a whole
/// channel).
/// </summary>
public class AdditionalYouTubeChannelDto
{
    [JsonPropertyName("youTubeChannelId")]
    public string? YouTubeChannelId { get; set; }

    [JsonPropertyName("youTubePlaylistId")]
    public string? YouTubePlaylistId { get; set; }
}
