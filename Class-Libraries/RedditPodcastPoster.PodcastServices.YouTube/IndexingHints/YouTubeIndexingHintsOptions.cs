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

    /// <summary>
    /// How much earlier than <see cref="FullWalkInterval"/> a full walk is already due. Passes run at the same minute
    /// each day, but the YouTube handler starts a few seconds earlier or later within the batch, so without this the
    /// daily walk would often miss its pass by seconds and wait for the next YouTube pass.
    /// </summary>
    public TimeSpan FullWalkSlack { get; set; } = TimeSpan.FromHours(1);

    /// <summary>The age of the last full walk at which the next one is due: the interval less the slack, never negative.</summary>
    public TimeSpan FullWalkDueAfter =>
        FullWalkInterval > FullWalkSlack ? FullWalkInterval - FullWalkSlack : TimeSpan.Zero;
}
