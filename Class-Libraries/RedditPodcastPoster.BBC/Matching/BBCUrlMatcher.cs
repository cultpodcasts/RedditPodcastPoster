namespace RedditPodcastPoster.BBC.Matching;

public static class BBCUrlMatcher
{
    public static bool IsBBCUrl(Uri url) =>
        url.Host.Contains("bbc.co.uk", StringComparison.OrdinalIgnoreCase);

    public static bool IsSoundsPlayUrl(Uri url) =>
        IsBBCUrl(url) && url.AbsolutePath.StartsWith("/sounds/play/", StringComparison.OrdinalIgnoreCase);

    public static bool IsIplayerEpisodeUrl(Uri url) =>
        IsBBCUrl(url) && url.AbsolutePath.StartsWith("/iplayer/episode", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sounds play and iPlayer episode pages only — not news or other bbc.co.uk hosts/paths.
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        IsSoundsPlayUrl(url) || IsIplayerEpisodeUrl(url);
}
