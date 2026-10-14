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

    // Heights the YouTube Data API reports per tier.
    public const int MaxresHeight = 720;
    public const int StandardHeight = 480;
    public const int HighHeight = 360;
    public const int MediumHeight = 180;
    public const int DefaultHeight = 90;

    private const int SofHeaderLength = 11;

    /// <summary>
    /// YouTube's grey "missing image" placeholder as measured on i.ytimg.com (2026-10-14): 120x90, 1097 bytes,
    /// served with 404 for a withdrawn maxresdefault / hq720.
    /// </summary>
    public static byte[] WithdrawnPlaceholderJpeg() => Jpeg(120, DefaultHeight, 1097);

    /// <summary>Measured sddefault for the same video: 640x480, 60207 bytes.</summary>
    public static byte[] StandardJpeg() => Jpeg(640, StandardHeight, 60207);

    /// <summary>Measured hqdefault: 480x360, 39854 bytes.</summary>
    public static byte[] HighJpeg() => Jpeg(480, HighHeight, 39854);

    /// <summary>Measured mqdefault: 320x180, 20678 bytes.</summary>
    public static byte[] MediumJpeg() => Jpeg(320, MediumHeight, 20678);

    /// <summary>A live 1280x720 maxresdefault.</summary>
    public static byte[] MaxresJpeg() => Jpeg(1280, MaxresHeight);

    /// <summary>A live 120x90 default.jpg.</summary>
    public static byte[] DefaultJpeg() => Jpeg(120, DefaultHeight);

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
