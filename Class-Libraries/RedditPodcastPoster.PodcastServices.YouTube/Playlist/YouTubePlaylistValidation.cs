namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public enum YouTubePlaylistValidationStatus
{
    /// <summary>No playlist id: nothing to check.</summary>
    NotApplicable,

    /// <summary>The playlist exists. Any owner channel is accepted.</summary>
    Valid,

    /// <summary>playlists.list returned nothing: the playlist does not exist or is private.</summary>
    PlaylistNotFound,

    /// <summary>
    /// The value is not a YouTube playlist id (wrong characters or length, e.g. a pasted URL or a stray '@').
    /// Refused before any YouTube call.
    /// </summary>
    InvalidPlaylistId,

    /// <summary>The YouTube lookup failed (quota on every key, network, API error). Retryable.</summary>
    LookupFailed
}

/// <summary>Result of checking a parent's YouTube playlist id.</summary>
public record YouTubePlaylistValidation(
    YouTubePlaylistValidationStatus Status,
    string? PlaylistId = null,
    string? OwnerChannelId = null,
    string? OwnerChannelTitle = null)
{
    public const string NotFoundCode = "youTubePlaylistNotFound";
    public const string LookupFailedCode = "youTubePlaylistLookupFailed";
    public const string InvalidIdCode = "youTubePlaylistIdInvalid";

    /// <summary>True when the id may be saved.</summary>
    public bool IsAllowed => Status is YouTubePlaylistValidationStatus.NotApplicable or YouTubePlaylistValidationStatus.Valid;

    /// <summary>True when the curator should retry (the check could not be made), rather than fix the id.</summary>
    public bool IsRetryable => Status == YouTubePlaylistValidationStatus.LookupFailed;

    /// <summary>Stable error code for the API response, or null when allowed.</summary>
    public string? Code => Status switch
    {
        YouTubePlaylistValidationStatus.PlaylistNotFound => NotFoundCode,
        YouTubePlaylistValidationStatus.LookupFailed => LookupFailedCode,
        YouTubePlaylistValidationStatus.InvalidPlaylistId => InvalidIdCode,
        _ => null
    };

    /// <summary>Curator-facing message, or null when allowed.</summary>
    public string? Message => Status switch
    {
        YouTubePlaylistValidationStatus.PlaylistNotFound =>
            $"YouTube playlist '{PlaylistId}' was not found (it does not exist or is private).",
        YouTubePlaylistValidationStatus.InvalidPlaylistId =>
            $"'{PlaylistId}' is not a YouTube playlist id (letters, digits, '-' and '_' only, 13-64 characters, e.g. PL...)." +
            (YouTubePlaylistIdFormat.TryExtractFromUrl(PlaylistId) is { } extracted
                ? $" Did you mean '{extracted}'?"
                : ""),
        YouTubePlaylistValidationStatus.LookupFailed =>
            $"Could not check YouTube playlist '{PlaylistId}' (YouTube lookup failed). Nothing was saved; try again.",
        _ => null
    };
}
