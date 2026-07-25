namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Declared ordering of a podcast's YouTube playlist. Absent (null) means order is probed from the
/// playlist head each windowed pass. <see cref="Arbitrary"/> marks manually curated playlists where
/// position carries no date information: new items may appear at either end, so the playlist must be
/// walked in full and filtered by added-at date. See docs/youtube-playlist-order.md.
/// </summary>
public enum PlaylistOrder
{
    ReverseChronological,
    Ascending,
    Arbitrary
}
