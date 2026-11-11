using System.Text.Json.Serialization;

namespace Api.Dtos;

public class NewsOrganisationDto : ServiceIdsDto
{
    /// <summary>
    /// From the organisation's <c>additionalYouTubeChannels</c> (#1370): the organisation's YouTube channels beside
    /// <c>youTubeChannelId</c>, each with its optional playlist on that channel. Empty when none.
    /// </summary>
    [JsonPropertyName("additionalYouTubeChannels")]
    public AdditionalYouTubeChannelDto[] AdditionalYouTubeChannels { get; set; } = [];

    /// <summary>
    /// The channel ids of <see cref="AdditionalYouTubeChannels"/>, in the same order (kept for clients that read the
    /// older channel-id-only shape; it does not say which channels are playlist-scoped). Empty when none.
    /// </summary>
    [JsonPropertyName("additionalYouTubeChannelIds")]
    public string[] AdditionalYouTubeChannelIds { get; set; } = [];
}
