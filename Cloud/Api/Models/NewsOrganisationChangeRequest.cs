namespace Api.Models;

public class NewsOrganisationChangeRequest : ServiceIdsChange
{
    /// <summary>
    /// Null = unchanged; a list (possibly empty) replaces <c>indexing.additionalYouTubeChannels</c> (#1370). Each entry is
    /// a channel id and an optional playlist id on that channel.
    /// </summary>
    public AdditionalYouTubeChannelChange[]? AdditionalYouTubeChannels { get; set; }

    /// <summary>
    /// Older channel-id-only form of <see cref="AdditionalYouTubeChannels"/>. It cannot set or clear a playlist: a channel
    /// that is already stored keeps its playlist and flags, and a new id is stored as a whole channel (no playlist). An id
    /// left out is removed. Null = unchanged. Sending both forms is refused (400).
    /// </summary>
    public string[]? AdditionalYouTubeChannelIds { get; set; }
}

/// <summary>One requested additional YouTube channel: its channel id and optional playlist on that channel.</summary>
public sealed record AdditionalYouTubeChannelChange(string? YouTubeChannelId, string? YouTubePlaylistId = null);
