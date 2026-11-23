using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// One entry in a PATCH <c>additionalYouTube</c> list (RPP #1411). An existing upload is identified by
/// <c>youTubeId</c> (url optional). A new upload is added from an absolute YouTube <c>url</c>.
/// </summary>
public sealed class YouTubeAlternateChangeRequest
{
    [JsonPropertyName("youTubeId")]
    public string? YouTubeId { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("channelName")]
    public string? ChannelName { get; set; }
}
