using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.YouTube;

namespace RedditPodcastPoster.Search.Formatting;

/// <summary>
/// One entry of the search document's <c>youTubeAlternates</c> field (RPP #1411):
/// <c>{youTubeId}|{release}|{channelName}</c>. Alternates have no search documents of their own (the index is at
/// the Free-tier quota), so the item's document carries just enough for the public page: the video id (player
/// fallback and the watch URL), an optional publish date (<c>yyyy-MM-dd</c>, UTC), and the channel name last so a
/// <c>|</c> in it needs no escaping (a reader splits on the first two <c>|</c> only). Retrievable, not searchable.
/// Null when the item has none (the field is not sent). An empty <c>release</c> or <c>channelName</c> is left blank
/// (<c>id||</c> or <c>id|2024-01-01|</c>).
/// </summary>
public static class YouTubeAlternateSearchToken
{
    public const char Separator = '|';

    public static string Format(YouTubeAlternate alternate)
    {
        ArgumentNullException.ThrowIfNull(alternate);
        var release = alternate.Release is { } at
            ? at.ToUniversalTime().ToString("yyyy-MM-dd")
            : string.Empty;
        var channel = (alternate.ChannelName ?? string.Empty).Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        return $"{alternate.YouTubeId}{Separator}{release}{Separator}{channel}";
    }

    /// <summary>The item's tokens in display order, or null when it has none (so the field is not sent).</summary>
    public static string[]? For(IPlayable item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.AdditionalYouTube is { Count: > 0 } alternates
            ? alternates.Select(Format).ToArray()
            : null;
    }
}
