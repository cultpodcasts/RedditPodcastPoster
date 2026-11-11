using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>PATCH body for <c>newsorganisation/{id}</c>: the shared publisher fields and the service ids.</summary>
public class NewsOrganisationChangeRequest : ServiceIdsChangeRequest
{
    /// <summary>
    /// The organisation's further YouTube channels beside <c>youTubeChannelId</c> (#1370), each indexed and owned as a
    /// whole channel. Omitted or null: unchanged. A list replaces the stored list (trimmed, blanks and repeats
    /// dropped); <c>[]</c> clears it. Each id must be a channel id (<c>UC</c> + 22 characters) and must not be the main
    /// <c>youTubeChannelId</c> (400), and must not be held by another submit target (409).
    /// </summary>
    [JsonPropertyName("additionalYouTubeChannelIds")]
    public string[]? AdditionalYouTubeChannelIds { get; set; }
}
