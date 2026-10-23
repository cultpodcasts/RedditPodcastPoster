namespace RedditPodcastPoster.Search.Formatting;

public static class Constants
{
    /// <summary>
    /// Indexed episode description cap (characters, including the ellipsis).
    /// Cap 50 was set on 22 Oct 2026 after cap 100 filled the Free-tier index
    /// (52.06 MB of ~52 MB at 88,279 documents). With usage at ~41.55 MB / 50 MB
    /// (≈8.45 MB headroom), cap 80 restores readable cards without repeating that
    /// overrun: docs estimate ~2.4 MB per +50 chars; the Oct 22 100→50 drop was
    /// closer to ~10 MB, so +30 chars is estimated at ~+1.5–6 MB (≈43–48 MB total).
    /// Cap 180/230 would not fit that headroom. Website cards already clamp at 180.
    /// Changing the cap needs a search teardown and rebuild so existing documents grow too.
    /// </summary>
    public const int DescriptionSize = 80;
}
