namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Which catalogue row owns a discovered episode when several podcasts share a service id.
/// A playlist-scoped podcast owns its playlist. A channel-wide podcast (no playlist id) is a different show.
/// A removed row suppresses discovery and save for its own show, including when another row of that same show
/// is still indexed. It does not suppress a live show that only shares the channel.
/// </summary>
public static class PodcastShowOwnership
{
    public static bool RemovedShowSuppresses(IEnumerable<Podcast> podcasts) =>
        RemovedShowSuppresses(podcasts.Select(podcast => (podcast.IsRemoved(), podcast.YouTubePlaylistId)));

    public static bool RemovedShowSuppresses(IEnumerable<(bool Removed, string PlaylistId)> rows)
    {
        var matches = rows.ToArray();
        return matches.Where(row => row.Removed).Any(removed =>
            matches.Where(row => !row.Removed).All(live => SameShow(removed.PlaylistId, live.PlaylistId)));
    }

    /// <summary>Channel-wide rows (no playlist) are one show. The same playlist id is one show.</summary>
    public static bool SameShow(string leftPlaylistId, string rightPlaylistId)
    {
        if (leftPlaylistId.Length == 0 && rightPlaylistId.Length == 0)
        {
            return true;
        }

        return leftPlaylistId.Length > 0 &&
               string.Equals(leftPlaylistId, rightPlaylistId, StringComparison.Ordinal);
    }
}
