using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.EntitySearchIndexer;

/// <summary>
///     The cover-art projection for a single episode's search document.
///     <para>
///         The chosen image is simply the first available cover art, YouTube-first:
///         <c>image = youtube ?? spotify ?? apple ?? other</c>.
///     </para>
///     <para>
///         Compaction applies to <b>YouTube thumbnails only</b>. A standard
///         <c>i.ytimg.com</c> thumbnail for the episode's own video
///         (<c>maxresdefault</c>/<c>sddefault</c>/<c>hqdefault</c>) is stored as a short
///         <see cref="YoutubeImageVariant" /> and the URL is dropped; the client rebuilds it from
///         <c>youtubeId</c> + variant (see <c>docs/search-index-slimming-plan.md</c>). Spotify,
///         Apple, "other", and any non-standard YouTube URL are never compacted — their full URL is
///         kept in <see cref="Image" />.
///     </para>
///     <para>
///         <see cref="Image" /> is always a non-null string. When the winner is a compacted YouTube
///         thumbnail, or there is no image at all, it is <see cref="string.Empty" /> — never
///         <c>null</c>. Azure AI Search merge ignores <c>null</c> source values, so an empty string
///         is required to clear/overwrite a previously-indexed image URL.
///     </para>
/// </summary>
public readonly record struct SearchEpisodeImage(string Image, string? YoutubeImageVariant)
{
    public static SearchEpisodeImage From(EpisodeImages? images, string? youTubeId)
    {
        var image = images?.YouTube ?? images?.Spotify ?? images?.Apple ?? images?.Other;

        var youtubeVariant = CompactYoutubeVariant(image, youTubeId);
        return youtubeVariant != null
            ? new SearchEpisodeImage(string.Empty, youtubeVariant)
            : new SearchEpisodeImage(image?.ToString() ?? string.Empty, null);
    }

    /// <summary>
    ///     Returns the compact variant name if <paramref name="image" /> is a standard
    ///     <c>i.ytimg.com</c> thumbnail for <paramref name="youTubeId" />; otherwise <c>null</c>
    ///     (meaning: keep the URL, do not compact).
    /// </summary>
    private static string? CompactYoutubeVariant(Uri? image, string? youTubeId)
    {
        if (image == null ||
            string.IsNullOrWhiteSpace(youTubeId) ||
            !image.Host.Equals("i.ytimg.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var expectedPrefix = $"/vi/{youTubeId}/";
        if (!image.AbsolutePath.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return image.AbsolutePath[expectedPrefix.Length..] switch
        {
            "maxresdefault.jpg" => "maxres",
            "sddefault.jpg" => "sd",
            "hqdefault.jpg" => "hq",
            _ => null
        };
    }
}
