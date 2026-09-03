namespace RedditPodcastPoster.InternetArchive.Matching;

public static class InternetArchiveUrlMatcher
{
    public static bool IsInternetArchiveUrl(Uri url) =>
        url.Host.Contains("archive.org", StringComparison.OrdinalIgnoreCase);

    public static bool IsDetailsUrl(Uri url) =>
        IsInternetArchiveUrl(url) &&
        url.AbsolutePath.StartsWith("/details", StringComparison.OrdinalIgnoreCase);

    /// <summary>Item pages under /details only — not search or other archive.org paths.</summary>
    public static bool IsSubmitUrl(Uri url) => IsDetailsUrl(url);
}
