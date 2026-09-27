using RedditPodcastPoster.Models.ContentKinds;

namespace RedditPodcastPoster.UrlShortening.Extensions;

/// <summary>
/// Public short id for routes and the shortener. Podcast episodes stay an unprefixed
/// 16-byte GUID. Film, TV, and News prepend ASCII f, t, or n before the same GUID bytes.
/// </summary>
public static class CatalogueShortId
{
    public const string Film = nameof(ContentKind.Film);
    public const string TvShowEpisode = nameof(ContentKind.TvShowEpisode);
    public const string NewsReport = nameof(ContentKind.NewsReport);

    /// <summary>
    /// A missing kind and Episode stay the unprefixed podcast id.
    /// Film, TV, and News are prefixed. Any other string throws.
    /// </summary>
    public static string Encode(Guid id, string? contentKind)
    {
        if (IsUnprefixedEpisode(contentKind))
        {
            return GuidExtensions.ToBase64(id);
        }

        if (!TryPrefix(contentKind, out var prefix))
        {
            throw UnknownKind(contentKind);
        }

        var guidBytes = id.ToByteArray();
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
            null or nameof(ContentKind.Episode) => "podcast",
            Film => "film",
            TvShowEpisode => "tv",
            NewsReport => "news",
            _ => throw UnknownKind(contentKind)
        };
        var shortId = Encode(id, contentKind);
        return $"/{root}/{EncodePlayableSlug(slug)}/{shortId}";
    }

    /// <summary>
    /// Match the site <c>encodeURIComponent</c> contract. <see cref="Uri.EscapeDataString"/>
    /// also encodes <c>!</c> <c>'</c> <c>(</c> <c>)</c> <c>*</c>, which the site leaves literal.
    /// </summary>
    private static string EncodePlayableSlug(string slug) =>
        Uri.EscapeDataString(slug)
            .Replace("%21", "!")
            .Replace("%27", "'")
            .Replace("%28", "(")
            .Replace("%29", ")")
            .Replace("%2A", "*");

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

    private static bool IsUnprefixedEpisode(string? contentKind) =>
        contentKind is null || contentKind == nameof(ContentKind.Episode);

    private static ArgumentException UnknownKind(string? contentKind) =>
        new($"Unknown catalogue content kind \"{contentKind}\".");

    private static bool TryPrefix(string? contentKind, out byte prefix)
    {
        prefix = contentKind switch
        {
            nameof(ContentKind.Film) => (byte)'f',
            nameof(ContentKind.TvShowEpisode) => (byte)'t',
            nameof(ContentKind.NewsReport) => (byte)'n',
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
