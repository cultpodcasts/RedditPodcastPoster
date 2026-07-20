using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.EntitySearchIndexer;

/// <summary>
///     The cover-art projection for a single episode's search document.
///     <para>
///         The image is simply the first available cover art, YouTube-first, stored <b>as-is</b>:
///         <c>image = youtube ?? spotify ?? apple ?? other</c>.
///     </para>
///     <para>
///         Whatever URL is selected is stored verbatim — including the full YouTube thumbnail URL
///         when YouTube wins. This projection does <b>not</b> inspect, classify, or rewrite the
///         YouTube thumbnail quality (maxresdefault / hqdefault / sddefault / default / …); the
///         selected image is never second-guessed.
///     </para>
///     <para>
///         <see cref="Image" /> is always a non-null string. When there is no image at all it is
///         <see cref="string.Empty" /> — never <c>null</c>. Azure AI Search merge ignores
///         <c>null</c> source values, so an empty string is required to clear/overwrite a
///         previously-indexed image URL (e.g. a stale Spotify cover from before a YouTube URL was
///         merged onto the episode). A real coalesced URL likewise overwrites the stale value.
///     </para>
/// </summary>
public readonly record struct SearchEpisodeImage(string Image)
{
    public static SearchEpisodeImage From(EpisodeImages? images)
    {
        var image = images?.YouTube ?? images?.Spotify ?? images?.Apple ?? images?.Other;
        return new SearchEpisodeImage(image?.ToString() ?? string.Empty);
    }
}
