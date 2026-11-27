using RedditPodcastPoster.Models.ContentKinds;

namespace RedditPodcastPoster.UrlShortening.Extensions;

/// <summary>
/// Public short id for routes and the shortener. Podcast episodes stay an unprefixed
/// 16-byte GUID. Film, TV, and News prepend ASCII f, t, or n before the same GUID bytes.
/// Domain <see cref="ContentKind"/> is the canonical API; string overloads parse names
/// (and null = Episode) for wire/decode callers that still hold kind names.
/// </summary>
public static class CatalogueShortId
{
    public const string Film = nameof(ContentKind.Film);
    public const string TvShowEpisode = nameof(ContentKind.TvShowEpisode);
    public const string NewsReport = nameof(ContentKind.NewsReport);

    /// <summary>
    /// Episode stays the unprefixed podcast id. Film, TV, and News are prefixed.
    /// </summary>
    public static string Encode(Guid id, ContentKind contentKind)
    {
        if (contentKind == ContentKind.Episode)
        {
            return GuidExtensions.ToBase64(id);
        }

        if (!TryPrefix(contentKind, out var prefix))
        {
            throw UnknownKind(contentKind.ToString());
        }

        var guidBytes = id.ToByteArray();
        var payload = new byte[guidBytes.Length + 1];
        payload[0] = prefix;
        guidBytes.CopyTo(payload, 1);
        return ToUrlBase64(payload);
    }

    /// <summary>
    /// Parses a kind name (or null = Episode) then encodes. Prefer
    /// <see cref="Encode(Guid, ContentKind)"/> when the caller already has the enum.
    /// </summary>
    public static string Encode(Guid id, string? contentKind) =>
        Encode(id, ParseContentKind(contentKind));

    /// <summary>
    /// Public playable path. <see cref="ContentKind.Episode"/> maps to <c>/podcast</c>
    /// with an unprefixed short id; Film, TV, and News use their route and prefixed id.
    /// </summary>
    public static string PlayablePath(string slug, Guid id, ContentKind contentKind)
    {
        var root = contentKind switch
        {
            ContentKind.Episode => "podcast",
            ContentKind.Film => "film",
            ContentKind.TvShowEpisode => "tv",
            ContentKind.NewsReport => "news",
            _ => throw UnknownKind(contentKind.ToString())
        };
        return $"/{root}/{EncodePlayableSlug(slug)}/{Encode(id, contentKind)}";
    }

    /// <summary>
    /// Parses a kind name (or null = Episode) then builds the path. Prefer
    /// <see cref="PlayablePath(string, Guid, ContentKind)"/> when the caller already has the enum.
    /// </summary>
    public static string PlayablePath(string slug, Guid id, string? contentKind) =>
        PlayablePath(slug, id, ParseContentKind(contentKind));

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

    /// <summary>
    /// Null and <c>Episode</c> mean podcast episode. Other names must match
    /// <see cref="Film"/>, <see cref="TvShowEpisode"/>, or <see cref="NewsReport"/>.
    /// </summary>
    public static ContentKind ParseContentKind(string? contentKind) =>
        contentKind switch
        {
            null or nameof(ContentKind.Episode) => ContentKind.Episode,
            Film => ContentKind.Film,
            TvShowEpisode => ContentKind.TvShowEpisode,
            NewsReport => ContentKind.NewsReport,
            _ => throw UnknownKind(contentKind)
        };

    private static string ToUrlBase64(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace("/", "-").Replace("+", "_").Replace("=", "");

    private static ArgumentException UnknownKind(string? contentKind) =>
        new($"Unknown catalogue content kind \"{contentKind}\".");

    private static bool TryPrefix(ContentKind contentKind, out byte prefix)
    {
        prefix = contentKind switch
        {
            ContentKind.Film => (byte)'f',
            ContentKind.TvShowEpisode => (byte)'t',
            ContentKind.NewsReport => (byte)'n',
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
