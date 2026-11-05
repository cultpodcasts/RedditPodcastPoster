using Api.Models;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;

namespace Api.Services.YouTube;

/// <summary>Which parent is being saved; claimants of every other kind are checked.</summary>
public enum ChannelWideYouTubeClaimKind
{
    Podcast,
    NewsOrganisation,
    TvShow
}

/// <summary>
/// A channel-wide YouTube claim (channel id, blank playlist) that would clash with a live claimant of another kind
/// on the same channel. Refuse the save with HTTP 400 and <see cref="Code"/>.
/// </summary>
public sealed record ChannelWideYouTubeClaimConflict(
    string ChannelId,
    IReadOnlyList<Guid> ConflictingIds)
{
    public const string Code = "channelWideYouTubeConflict";

    public string Message =>
        $"YouTube channel '{ChannelId}' is already claimed channel-wide (no playlist) by {string.Join(", ", ConflictingIds)}. " +
        "Give this parent a youTubePlaylistId so the claim is playlist-scoped.";
}

/// <summary>
/// Save-time guard: refuse a channel-wide podcast on a channel a channel-wide news organisation or TV show already
/// claims, and the reverse. Playlist-scoped parents may share a channel. Removed claimants are ignored.
/// Checks the projected post-apply channel + playlist (including a clear-playlist that would create the clash).
/// </summary>
public interface IChannelWideYouTubeClaimGuard
{
    /// <summary>
    /// Null when the save may go ahead. Otherwise a conflict naming the live channel-wide claimants of other kinds.
    /// </summary>
    Task<ChannelWideYouTubeClaimConflict?> Check(
        Guid entityId,
        ChannelWideYouTubeClaimKind kind,
        string? projectedChannelId,
        string? projectedPlaylistId,
        bool projectedRemoved,
        CancellationToken cancellationToken = default);
}

/// <summary>Post-apply YouTube channel / playlist / removed projection for the claim guard.</summary>
public static class ChannelWideYouTubeClaimProjection
{
    public static (string? ChannelId, string? PlaylistId, bool Removed) ForPodcast(
        Podcast podcast,
        PodcastChangeRequest change)
    {
        var channel = change.YouTubeChannelId != null
            ? (string.IsNullOrWhiteSpace(change.YouTubeChannelId) ? string.Empty : change.YouTubeChannelId.Trim())
            : podcast.YouTubeChannelId;
        var playlist = change.YouTubePlaylistId != null
            ? (string.IsNullOrWhiteSpace(change.YouTubePlaylistId) ? string.Empty : change.YouTubePlaylistId.Trim())
            : podcast.YouTubePlaylistId;
        var removed = change.Removed ?? podcast.IsRemoved();
        return (channel, playlist, removed);
    }

    public static (string? ChannelId, string? PlaylistId, bool Removed) ForParent(
        CatalogueIndexingSource? indexing,
        bool currentlyRemoved,
        ServiceIdsChange change)
    {
        var channel = change.YouTubeChannelId != null
            ? NullIfBlank(change.YouTubeChannelId)
            : indexing?.YouTubeChannelId;
        var playlist = change.YouTubePlaylistId != null
            ? (string.IsNullOrWhiteSpace(change.YouTubePlaylistId) ? null : change.YouTubePlaylistId.Trim())
            : indexing?.YouTubePlaylistId;
        var removed = change.Removed ?? currentlyRemoved;
        return (channel, playlist, removed);
    }

    private static string? NullIfBlank(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 ? trimmed : null;
    }
}
