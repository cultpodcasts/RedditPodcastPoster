namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public enum YouTubePlaylistOwnershipStatus
{
    /// <summary>No playlist, or no channel id to compare it with: nothing to check.</summary>
    NotApplicable,

    /// <summary>The playlist is owned by the parent's YouTube channel.</summary>
    Matches,

    /// <summary>The playlist is owned by a different channel. The indexer would drop every video in it.</summary>
    Mismatch,

    /// <summary>playlists.list returned nothing: the playlist does not exist or is private.</summary>
    PlaylistNotFound,

    /// <summary>The YouTube lookup failed (quota on every key, network, API error). Retryable.</summary>
    LookupFailed
}

/// <summary>Result of checking that a YouTube playlist belongs to a parent's YouTube channel.</summary>
public record YouTubePlaylistOwnership(
    YouTubePlaylistOwnershipStatus Status,
    string? ChannelId = null,
    string? PlaylistId = null,
    string? OwnerChannelId = null,
    string? OwnerChannelTitle = null)
{
    public const string MismatchCode = "youTubePlaylistChannelMismatch";
    public const string NotFoundCode = "youTubePlaylistNotFound";
    public const string LookupFailedCode = "youTubePlaylistOwnerLookupFailed";

    /// <summary>True when the pair may be saved.</summary>
    public bool IsAllowed => Status is YouTubePlaylistOwnershipStatus.NotApplicable or YouTubePlaylistOwnershipStatus.Matches;

    /// <summary>True when the curator should retry (the check could not be made), rather than fix the ids.</summary>
    public bool IsRetryable => Status == YouTubePlaylistOwnershipStatus.LookupFailed;

    /// <summary>Stable error code for the API response, or null when allowed.</summary>
    public string? Code => Status switch
    {
        YouTubePlaylistOwnershipStatus.Mismatch => MismatchCode,
        YouTubePlaylistOwnershipStatus.PlaylistNotFound => NotFoundCode,
        YouTubePlaylistOwnershipStatus.LookupFailed => LookupFailedCode,
        _ => null
    };

    /// <summary>Curator-facing message, or null when allowed.</summary>
    public string? Message => Status switch
    {
        YouTubePlaylistOwnershipStatus.Mismatch =>
            $"YouTube playlist '{PlaylistId}' belongs to channel '{OwnerChannelId}'" +
            (string.IsNullOrWhiteSpace(OwnerChannelTitle) ? "" : $" ({OwnerChannelTitle})") +
            $", not the YouTube channel id '{ChannelId}'. Set the YouTube channel id to the playlist's channel, or use a playlist from channel '{ChannelId}'.",
        YouTubePlaylistOwnershipStatus.PlaylistNotFound =>
            $"YouTube playlist '{PlaylistId}' was not found (it does not exist or is private).",
        YouTubePlaylistOwnershipStatus.LookupFailed =>
            $"Could not check which channel owns YouTube playlist '{PlaylistId}' (YouTube lookup failed). Nothing was saved; try again.",
        _ => null
    };
}
