namespace RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

/// <summary>
/// Recognises YouTube thumbnail tiers from an <c>i.ytimg.com/vi/{id}/{tier}.jpg</c> URL.
/// </summary>
public static class YouTubeThumbnailUrls
{
    private const string DefaultTierFileName = "default";

    /// <summary>
    /// Tiers YouTube can withdraw after indexing (for example when a creator's custom thumbnail
    /// replaces the auto-generated frame and no 1280-wide rendition is produced). A stored URL on
    /// one of these tiers is worth re-checking shortly after release.
    /// </summary>
    private static readonly string[] WithdrawableHighResolutionFileNames = ["maxresdefault", "hq720"];

    public static bool IsDefaultTier(Uri imageUrl) =>
        string.Equals(FileNameWithoutExtension(imageUrl), DefaultTierFileName, StringComparison.OrdinalIgnoreCase);

    public static bool IsWithdrawableHighResolution(Uri imageUrl) =>
        WithdrawableHighResolutionFileNames.Contains(
            FileNameWithoutExtension(imageUrl),
            StringComparer.OrdinalIgnoreCase);

    /// <remarks>Stored episode images are absolute URLs.</remarks>
    private static string FileNameWithoutExtension(Uri imageUrl) =>
        Path.GetFileNameWithoutExtension(imageUrl.AbsolutePath);
}
