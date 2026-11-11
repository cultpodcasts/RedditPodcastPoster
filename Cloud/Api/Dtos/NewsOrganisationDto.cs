using System.Text.Json.Serialization;

namespace Api.Dtos;

public class NewsOrganisationDto : ServiceIdsDto
{
    /// <summary>
    /// From <c>indexing.additionalYouTubeChannelIds</c> (#1370): the organisation's YouTube channels beside
    /// <c>youTubeChannelId</c>, each indexed as a whole channel. Empty when none.
    /// </summary>
    [JsonPropertyName("additionalYouTubeChannelIds")]
    public string[] AdditionalYouTubeChannelIds { get; set; } = [];
}
