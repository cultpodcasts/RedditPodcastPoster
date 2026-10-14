namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

/// <summary>
/// YouTube thumbnail URLs and minimal JPEG bodies for thumbnail probe rules.
/// </summary>
public static class YouTubeThumbnailSpecimens
{
    public const string MaxresTier = "maxresdefault";
    public const string Hq720Tier = "hq720";
    public const string StandardTier = "sddefault";
    public const string HighTier = "hqdefault";
    public const string MediumTier = "mqdefault";
    public const string DefaultTier = "default";

    private const int SofHeaderLength = 11;

    public static Uri Url(string youTubeId, string tier) => new($"https://i.ytimg.com/vi/{youTubeId}/{tier}.jpg");

    /// <summary>
    /// A JPEG whose first SOF0 frame header carries <paramref name="width"/> x <paramref name="height"/>,
    /// zero-padded to <paramref name="totalBytes"/> so a body can match a measured CDN response size.
    /// </summary>
    public static byte[] Jpeg(int width, int height, int totalBytes = SofHeaderLength + 2)
    {
        var minimum = SofHeaderLength + 2;
        var body = new byte[Math.Max(totalBytes, minimum)];
        byte[] header =
        [
            0xFF, 0xD8, // SOI
            0xFF, 0xC0, 0x00, 0x11, 0x08, // SOF0, segment length, precision
            (byte)(height >> 8), (byte)height,
            (byte)(width >> 8), (byte)width
        ];
        header.CopyTo(body, 0);
        body[^2] = 0xFF;
        body[^1] = 0xD9; // EOI
        return body;
    }
}
