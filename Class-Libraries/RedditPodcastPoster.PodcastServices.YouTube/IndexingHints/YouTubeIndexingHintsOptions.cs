namespace RedditPodcastPoster.PodcastServices.YouTube.IndexingHints;

/// <summary>Settings for the YouTube indexing hints (configuration section <see cref="SectionName"/>).</summary>
public sealed class YouTubeIndexingHintsOptions
{
    public const string SectionName = "youTubeIndexingHints";

    /// <summary>
    /// A playlist needs more items than this (its last <c>playlistItemCount</c>) before a pass checks the channel's
    /// uploads instead of walking it. Smaller playlists are walked every pass and keep no hints.
    /// </summary>
    public int UploadsHeadMinimumPlaylistItems { get; set; } = 150;

    /// <summary>How long a full walk is trusted. After this a pass walks the whole playlist again (daily by default).</summary>
    public TimeSpan FullWalkInterval { get; set; } = TimeSpan.FromHours(24);
}
