namespace RedditPodcastPoster.Search.Formatting;

public static class Constants
{
    /// <summary>
    /// Indexed episode description cap (characters, including the ellipsis).
    /// 100 targets about 45 MB on the Free cultpodcasts index: at a 180 cap
    /// the live index was 54.95 MB / 86k docs, and a sample showed descriptions
    /// averaging 158 characters with most already at the cap.
    /// </summary>
    public const int DescriptionSize = 100;
}