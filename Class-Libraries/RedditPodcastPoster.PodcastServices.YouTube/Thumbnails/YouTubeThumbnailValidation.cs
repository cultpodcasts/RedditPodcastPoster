namespace RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

internal static class YouTubeThumbnailValidation
{
    public const int PlaceholderMaxHeight = 90;

    public static bool IsUsableThumbnail(int width, int height, bool isDefaultTier)
    {
        if (isDefaultTier)
        {
            return width > 0 && height > 0;
        }

        return height > PlaceholderMaxHeight;
    }
}
