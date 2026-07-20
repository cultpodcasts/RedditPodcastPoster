using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.EntitySearchIndexer;

/// <summary>
///     The cover-art projection for a single episode's search document.
///     <para>
///         The chosen image is the first available cover art, YouTube-first:
///         <c>image = youtube ?? spotify ?? apple ?? other</c>.
///     </para>
///     <para>
///         Two platforms whose URLs are a fixed prefix plus a single opaque token are
///         <b>compacted</b> so the search index stores only the token and the client rebuilds the
///         URL:
///         <list type="bullet">
///             <item>
///                 <b>YouTube</b> — a standard <c>i.ytimg.com/vi/{youTubeId}/{quality}.jpg</c>
///                 thumbnail for this episode's own video is stored as a short
///                 <see cref="YoutubeImageVariant" /> (<c>maxres</c>/<c>sd</c>/<c>hq</c>). The
///                 variant is mapped from the <b>existing filename as-is</b> — the selected quality
///                 is never upgraded, downgraded, or second-guessed.
///             </item>
///             <item>
///                 <b>Spotify</b> — a standard <c>i.scdn.co/image/{id}</c> cover is stored as its
///                 <see cref="SpotifyImageId" /> (the single <c>{id}</c> path segment). The prefix
///                 is fixed and the id is opaque, so this is losslessly reversible.
///             </item>
///         </list>
///         Apple artwork (<c>is{1-5}-ssl.mzstatic.com/image/thumb/…/{dims}bb.jpg</c>) has a variable
///         host and a deep, variable path with no single reversible token, so it is <b>never</b>
///         compacted. "Other" and any non-standard YouTube/Spotify URL are likewise kept in full in
///         <see cref="Image" />.
///     </para>
///     <para>
///         <see cref="Image" />, <see cref="YoutubeImageVariant" /> and <see cref="SpotifyImageId" />
///         are always non-null strings. When a value does not apply it is <see cref="string.Empty" />
///         — never <c>null</c>. Azure AI Search merge ignores <c>null</c> source values, so an empty
///         string is required to clear/overwrite a previously-indexed value on incremental
///         (high-water-mark) merge (e.g. a stale Spotify cover left in <c>image</c> from before a
///         YouTube video was merged onto the episode, or a stale compact token).
///     </para>
/// </summary>
public readonly record struct SearchEpisodeImage(string Image, string YoutubeImageVariant, string SpotifyImageId)
{
    public static SearchEpisodeImage From(EpisodeImages? images, string? youTubeId)
    {
        var image = images?.YouTube ?? images?.Spotify ?? images?.Apple ?? images?.Other;

        var youtubeVariant = TryYoutubeVariant(image, youTubeId);
        if (youtubeVariant != null)
        {
            return new SearchEpisodeImage(string.Empty, youtubeVariant, string.Empty);
        }

        var spotifyImageId = TrySpotifyImageId(image);
        if (spotifyImageId != null)
        {
            return new SearchEpisodeImage(string.Empty, string.Empty, spotifyImageId);
        }

        return new SearchEpisodeImage(image?.ToString() ?? string.Empty, string.Empty, string.Empty);
    }

    /// <summary>
    ///     Maps a standard <c>i.ytimg.com/vi/{youTubeId}/{quality}.jpg</c> thumbnail to its compact
    ///     variant, using the filename exactly as it appears — the selected quality is preserved, not
    ///     re-evaluated. Returns <c>null</c> (meaning "keep the full URL") for any other URL.
    /// </summary>
    private static string? TryYoutubeVariant(Uri? image, string? youTubeId)
    {
        if (image == null ||
            string.IsNullOrWhiteSpace(youTubeId) ||
            !image.Host.Equals("i.ytimg.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var prefix = $"/vi/{youTubeId}/";
        if (!image.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return image.AbsolutePath[prefix.Length..] switch
        {
            "maxresdefault.jpg" => "maxres",
            "sddefault.jpg" => "sd",
            "hqdefault.jpg" => "hq",
            _ => null
        };
    }

    /// <summary>
    ///     Extracts the <c>{id}</c> from a standard <c>i.scdn.co/image/{id}</c> Spotify cover. Returns
    ///     <c>null</c> (meaning "keep the full URL") for anything with a query string, extra path
    ///     segment, or different host.
    /// </summary>
    private static string? TrySpotifyImageId(Uri? image)
    {
        if (image == null ||
            !image.Host.Equals("i.scdn.co", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(image.Query))
        {
            return null;
        }

        const string prefix = "/image/";
        if (!image.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var id = image.AbsolutePath[prefix.Length..];
        return id.Length > 0 && !id.Contains('/') ? id : null;
    }
}
