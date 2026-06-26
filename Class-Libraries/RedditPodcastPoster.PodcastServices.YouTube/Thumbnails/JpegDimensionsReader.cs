namespace RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

internal static class JpegDimensionsReader
{
    public static bool TryGetDimensions(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;

        for (var i = 0; i < data.Length - 8; i++)
        {
            if (data[i] != 0xFF)
            {
                continue;
            }

            var marker = data[i + 1];
            if (marker is < 0xC0 or > 0xCF or 0xC4 or 0xC8 or 0xCC)
            {
                continue;
            }

            height = (data[i + 5] << 8) | data[i + 6];
            width = (data[i + 7] << 8) | data[i + 8];
            return width > 0 && height > 0;
        }

        return false;
    }
}
