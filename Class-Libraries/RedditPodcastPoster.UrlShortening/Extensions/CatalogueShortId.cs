namespace RedditPodcastPoster.UrlShortening.Extensions;

/// <summary>
/// Public short id for routes and the shortener. Podcast episodes stay an unprefixed
/// 16-byte GUID. Film, TV, and News prepend ASCII f, t, or n before the same GUID bytes.
/// </summary>
public static class CatalogueShortId
{
    public const string Film = "Film";
    public const string TvShowEpisode = "TvShowEpisode";
    public const string NewsReport = "NewsReport";

    public static string Encode(Guid id, string? contentKind)
    {
        var guidBytes = id.ToByteArray();
        if (!TryPrefix(contentKind, out var prefix))
        {
            return GuidExtensions.ToBase64(id);
        }

        var payload = new byte[guidBytes.Length + 1];
        payload[0] = prefix;
        guidBytes.CopyTo(payload, 1);
        return ToUrlBase64(payload);
    }

    /// <summary>
    /// Public playable path. Podcast stays /podcast and an unprefixed short id.
    /// Film, TV, and News use their route and the prefixed short id.
    /// </summary>
    public static string PlayablePath(string slug, Guid id, string? contentKind)
    {
        var root = contentKind switch
        {
            Film => "film",
            TvShowEpisode => "tv",
            NewsReport => "news",
            _ => "podcast"
        };
        var shortId = root == "podcast" ? Encode(id, null) : Encode(id, contentKind);
        return $"/{root}/{Uri.EscapeDataString(slug)}/{shortId}";
    }

    /// <summary>
    /// An existing unprefixed short link still identifies the same id.
    /// When that id is now a Film, TV episode, or news report, the link target is the new path.
    /// </summary>
    public static string? MovedPlayablePath(string slug, Guid id, string? resolvedKind) =>
        resolvedKind is Film or TvShowEpisode or NewsReport
            ? PlayablePath(slug, id, resolvedKind)
            : null;

    public static bool TryDecode(string shortId, out Guid id, out string? contentKind)
    {
        id = Guid.Empty;
        contentKind = null;
        if (string.IsNullOrWhiteSpace(shortId))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            var padded = shortId.Replace('-', '/').Replace('_', '+');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            bytes = Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return false;
        }

        if (bytes.Length == 16)
        {
            id = new Guid(bytes);
            return true;
        }

        if (bytes.Length == 17 && TryKind(bytes[0], out contentKind))
        {
            id = new Guid(bytes.AsSpan(1));
            return true;
        }

        return false;
    }

    private static string ToUrlBase64(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace("/", "-").Replace("+", "_").Replace("=", "");

    private static bool TryPrefix(string? contentKind, out byte prefix)
    {
        prefix = contentKind switch
        {
            Film => (byte)'f',
            TvShowEpisode => (byte)'t',
            NewsReport => (byte)'n',
            _ => 0
        };
        return prefix != 0;
    }

    private static bool TryKind(byte prefix, out string? contentKind)
    {
        contentKind = prefix switch
        {
            (byte)'f' => Film,
            (byte)'t' => TvShowEpisode,
            (byte)'n' => NewsReport,
            _ => null
        };
        return contentKind != null;
    }
}
