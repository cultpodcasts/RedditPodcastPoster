using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.EntitySearchIndexer;

/// <summary>
///     The cover-art projection for a single episode's search document.
///     <para>
///         The image is the first available cover art, YouTube-first:
///         <c>image = youtube ?? spotify ?? apple ?? other</c>.
///     </para>
///     <para>
///         <b>Loss-less compaction.</b> The three platforms whose URL is a fixed prefix plus an
///         opaque tail are stored as a short <i>token</i> in <see cref="Image" />; the client
///         (<c>expandImage()</c>) rebuilds the <b>exact</b> original URL. A token is only ever emitted
///         when <see cref="Expand" /> reproduces the original URL byte-for-byte
///         (<see cref="Compact" /> verifies this round-trip), so the selected/probed image is never
///         lost or changed. The token grammar (first character is the scheme sigil):
///         <list type="bullet">
///             <item>
///                 <b>YouTube</b> <c>y{q}</c> — a standard
///                 <c>https://i.ytimg.com/vi/{youTubeId}/{quality}.jpg</c> thumbnail for this
///                 episode's own video. The video id is dropped (it equals the document's
///                 <c>youtubeId</c>) and the quality is one character:
///                 <c>x</c>=maxresdefault, <c>s</c>=sddefault, <c>h</c>=hqdefault,
///                 <c>m</c>=mqdefault, <c>d</c>=default. Every quality the YouTube Data API can
///                 return is representable, so the exact probed quality is preserved — it is
///                 <i>recorded</i>, never re-picked, upgraded, or downgraded.
///             </item>
///             <item>
///                 <b>Spotify</b> <c>s{id}</c> — a standard <c>https://i.scdn.co/image/{id}</c> cover
///                 (single opaque segment, no query/fragment). Only the fixed prefix is dropped.
///             </item>
///             <item>
///                 <b>Apple</b> <c>a{n}{path}</c> — a standard
///                 <c>https://is{n}-ssl.mzstatic.com/image/thumb/{path}</c> artwork
///                 (<c>n</c> is the host digit 1-5). Only the fixed prefix is dropped; the deep
///                 <c>{path}</c> (which may contain <c>/</c>) is kept verbatim.
///             </item>
///         </list>
///         Anything that does not match one of these exact shapes — "other" art, an unusual
///         YouTube/Spotify/Apple URL, a thumbnail for a different video, a URL with a query string —
///         is kept as its <b>full URL</b> in <see cref="Image" />. Nothing lossy is ever dropped.
///     </para>
///     <para>
///         <b>Anti-placeholder guarantee is upstream and untouched.</b> The YouTube thumbnail stored
///         in <see cref="EpisodeImages.YouTube" /> is chosen at ingestion by
///         <c>YouTubeThumbnailResolver</c> (candidates highest-resolution-first, YouTube's grey
///         placeholder rejected). Compaction records that selection verbatim and the client rebuilds
///         the identical URL, so index-slimming can never resurrect a placeholder and never needs a
///         maxres&#8594;hqdefault client fallback.
///     </para>
///     <para>
///         <see cref="Image" /> is always a non-null string. When there is no image it is
///         <see cref="string.Empty" /> — never <c>null</c>. Azure AI Search merge ignores
///         <c>null</c> source values, so an empty string is required to clear/overwrite a
///         previously-indexed image on incremental (high-water-mark) merge (e.g. a stale Spotify
///         cover left behind before a YouTube video was merged onto the episode).
///     </para>
/// </summary>
public readonly record struct SearchEpisodeImage(string Image)
{
    private const string YouTubePrefix = "https://i.ytimg.com/vi/";
    private const string SpotifyPrefix = "https://i.scdn.co/image/";
    private const string AppleScheme = "https://is";
    private const string AppleHostTail = "-ssl.mzstatic.com/image/thumb/";

    // Standard thumbnail filename <-> single-char quality code. Ordered specific-first so the
    // ENDSWITH-style matching in the mirrored Cosmos SQL and this table agree.
    private static readonly (string FileName, char Code)[] YouTubeQualities =
    [
        ("maxresdefault.jpg", 'x'),
        ("sddefault.jpg", 's'),
        ("hqdefault.jpg", 'h'),
        ("mqdefault.jpg", 'm'),
        ("default.jpg", 'd')
    ];

    public static SearchEpisodeImage From(EpisodeImages? images, string? youTubeId)
    {
        var image = images?.YouTube ?? images?.Spotify ?? images?.Apple ?? images?.Other;
        if (image is null)
        {
            return new SearchEpisodeImage(string.Empty);
        }

        var url = image.ToString();
        return new SearchEpisodeImage(Compact(url, youTubeId) ?? url);
    }

    /// <summary>
    ///     Compacts a full image URL to a short token, or returns <c>null</c> to keep the full URL.
    ///     Guaranteed loss-less: a token is only returned when <see cref="Expand" /> reproduces the
    ///     original <paramref name="url" /> byte-for-byte.
    /// </summary>
    public static string? Compact(string url, string? youTubeId)
    {
        var token = TryYouTube(url, youTubeId) ?? TrySpotify(url) ?? TryApple(url);
        return token is not null && Expand(token, youTubeId) == url ? token : null;
    }

    /// <summary>
    ///     Expands a compact token back to its full URL — the C# mirror of the website
    ///     <c>expandImage()</c> helper. A value that is already a full URL (starts with <c>http</c>)
    ///     or empty is returned unchanged.
    /// </summary>
    public static string Expand(string image, string? youTubeId)
    {
        if (string.IsNullOrEmpty(image) || image.StartsWith("http", StringComparison.Ordinal))
        {
            return image;
        }

        var payload = image[1..];
        switch (image[0])
        {
            case 'y':
                foreach (var (fileName, code) in YouTubeQualities)
                {
                    if (payload.Length == 1 && payload[0] == code)
                    {
                        return $"{YouTubePrefix}{youTubeId}/{fileName}";
                    }
                }

                return image;
            case 's' when payload.Length > 0:
                return $"{SpotifyPrefix}{payload}";
            case 'a' when payload.Length >= 1:
                return $"{AppleScheme}{payload[0]}{AppleHostTail}{payload[1..]}";
            default:
                return image;
        }
    }

    private static string? TryYouTube(string url, string? youTubeId)
    {
        if (string.IsNullOrWhiteSpace(youTubeId))
        {
            return null;
        }

        var prefix = $"{YouTubePrefix}{youTubeId}/";
        if (!url.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var fileName = url[prefix.Length..];
        foreach (var (name, code) in YouTubeQualities)
        {
            if (fileName == name)
            {
                return $"y{code}";
            }
        }

        return null;
    }

    private static string? TrySpotify(string url)
    {
        if (!url.StartsWith(SpotifyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var id = url[SpotifyPrefix.Length..];
        if (id.Length == 0 || id.Contains('/') || id.Contains('?') || id.Contains('#'))
        {
            return null;
        }

        return $"s{id}";
    }

    private static string? TryApple(string url)
    {
        if (!url.StartsWith(AppleScheme, StringComparison.Ordinal) || url.Length <= AppleScheme.Length)
        {
            return null;
        }

        var digit = url[AppleScheme.Length];
        if (digit is < '1' or > '5')
        {
            return null;
        }

        var afterDigit = url[(AppleScheme.Length + 1)..];
        if (!afterDigit.StartsWith(AppleHostTail, StringComparison.Ordinal))
        {
            return null;
        }

        var path = afterDigit[AppleHostTail.Length..];
        return $"a{digit}{path}";
    }
}
