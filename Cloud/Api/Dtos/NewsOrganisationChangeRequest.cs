using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>PATCH body for <c>newsorganisation/{id}</c>: the shared publisher fields and the service ids.</summary>
public class NewsOrganisationChangeRequest : ServiceIdsChangeRequest
{
    /// <summary>
    /// The organisation's further YouTube channels beside <c>youTubeChannelId</c> (#1370). Each entry is
    /// <c>{ "youTubeChannelId": "UC…", "youTubePlaylistId": "PL…" }</c>; without a playlist the channel is indexed and
    /// owned whole, with one only that playlist's videos are (the playlist must belong to that channel). Omitted or null:
    /// unchanged. A list replaces the stored list; <c>[]</c> clears it. Entries with a blank channel id and no playlist
    /// are dropped, and an exact repeat is dropped. Refused (400): a malformed channel id or an entry with a playlist but
    /// no channel (<c>invalidAdditionalYouTubeChannelId</c>), the same channel listed twice with different playlists
    /// (<c>invalidAdditionalYouTubeChannelId</c>), the main <c>youTubeChannelId</c>
    /// (<c>additionalYouTubeChannelIsMainChannel</c>), a malformed / missing / foreign playlist
    /// (<c>youTubePlaylistIdInvalid</c>, <c>youTubePlaylistNotFound</c>, <c>youTubePlaylistChannelMismatch</c>), and a
    /// whole-channel claim another target already holds channel-wide (<c>channelWideYouTubeConflict</c>). A channel held
    /// by another submit target is 409. A playlist lookup that fails is 503 <c>youTubePlaylistLookupFailed</c>.
    /// </summary>
    [JsonPropertyName("additionalYouTubeChannels")]
    public AdditionalYouTubeChannelDto[]? AdditionalYouTubeChannels { get; set; }

    /// <summary>
    /// Older channel-id-only form (#1370): each id is stored as a whole channel with no playlist, with the same rules
    /// as <see cref="AdditionalYouTubeChannels"/>. Sending both fields is refused (400
    /// <c>invalidAdditionalYouTubeChannelId</c>).
    /// </summary>
    [JsonPropertyName("additionalYouTubeChannelIds")]
    public string[]? AdditionalYouTubeChannelIds { get; set; }
}
