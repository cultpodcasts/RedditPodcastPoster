namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

/// <summary>How a playlistItems.list walk ended. Only <see cref="Complete"/> may refresh YouTube indexing hints.</summary>
public enum YouTubePlaylistWalkOutcome
{
    /// <summary>
    /// The walk did not read the whole playlist: it failed, a page-walk guard stopped it short, or the arbitrary-order
    /// page cap tripped. Hints are left as they were.
    /// </summary>
    Incomplete,

    /// <summary>
    /// The walk read to the end: YouTube stopped returning a next-page token, or the guard stopped on an empty page
    /// after every item had been read (the repeated-token / empty-page tail of a finished playlist).
    /// </summary>
    Complete,

    /// <summary>
    /// A newest-first playlist stopped once its items left the date window. The head is cheap to read, so the
    /// playlist needs no hints.
    /// </summary>
    StoppedAtWindow
}
