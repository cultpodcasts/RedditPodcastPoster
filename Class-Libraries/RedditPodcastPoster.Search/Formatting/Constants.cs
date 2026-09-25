namespace RedditPodcastPoster.Search.Formatting;

public static class Constants
{
    /// <summary>
    /// Indexed episode description cap (characters, including the ellipsis).
    /// Cap 100 is the fitted width for the Free tier. A 120-cap refresh on
    /// 25 Sep 2026 grew the same index from 46.64 MB to 63.66 MB and then
    /// rejected further merges.
    /// </summary>
    public const int DescriptionSize = 100;
}
