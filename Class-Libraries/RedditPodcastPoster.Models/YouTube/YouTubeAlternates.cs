using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.YouTube;

/// <summary>
/// Helpers for the ordered <c>additionalYouTube</c> list (RPP #1411). Empty lists are stored as null
/// (omitted), the same as film trailers.
/// </summary>
public static class YouTubeAlternates
{
    public static bool Holds(IReadOnlyList<YouTubeAlternate>? alternates, string? youTubeId)
    {
        if (string.IsNullOrWhiteSpace(youTubeId) || alternates is not { Count: > 0 })
        {
            return false;
        }

        return alternates.Any(alternate =>
            string.Equals(alternate.YouTubeId, youTubeId, StringComparison.Ordinal));
    }

    public static bool HoldsUrl(IReadOnlyList<YouTubeAlternate>? alternates, Uri? url)
    {
        if (url is null || alternates is not { Count: > 0 })
        {
            return false;
        }

        var id = YouTubeIdResolver.Extract(url);
        if (!string.IsNullOrWhiteSpace(id))
        {
            return Holds(alternates, id);
        }

        return alternates.Any(alternate =>
            alternate.Url is not null &&
            string.Equals(alternate.Url.ToString().TrimEnd('/'), url.ToString().TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Appends <paramref name="alternate"/> when that video is not already in the list. Returns the list (or a new one).</summary>
    public static List<YouTubeAlternate> Add(IList<YouTubeAlternate>? current, YouTubeAlternate alternate)
    {
        ArgumentNullException.ThrowIfNull(alternate);
        var list = current is null ? [] : current.ToList();
        if (!Holds(list, alternate.YouTubeId))
        {
            list.Add(alternate);
        }

        return list;
    }

    /// <summary>Null when there are none, so the JSON field is omitted.</summary>
    public static List<YouTubeAlternate>? OrNull(IList<YouTubeAlternate>? current) =>
        current is { Count: > 0 } ? current.ToList() : null;

    /// <summary>
    /// Builds an alternate from a podcast episode's YouTube upload (Move to an existing film, or the
    /// youtube-alternate attach route). Null when the episode has no YouTube video id or url.
    /// </summary>
    public static YouTubeAlternate? FromEpisode(
        Episode episode,
        DateTime addedAtUtc,
        string? channelId = null,
        string? channelName = null)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ServiceLink? link = null;
        episode.Services?.TryGetValue(ServiceKeys.YouTube, out link);
        var url = link?.Url;
        var youTubeId = episode.Ids?.YouTube;
        if (string.IsNullOrWhiteSpace(youTubeId) && url is not null)
        {
            youTubeId = YouTubeIdResolver.Extract(url);
        }

        if (string.IsNullOrWhiteSpace(youTubeId) || url is null)
        {
            return null;
        }

        return new YouTubeAlternate
        {
            YouTubeId = youTubeId,
            Url = url,
            Image = link?.Image,
            Title = string.IsNullOrWhiteSpace(episode.Title) ? null : episode.Title,
            ChannelId = string.IsNullOrWhiteSpace(channelId) ? null : channelId,
            ChannelName = string.IsNullOrWhiteSpace(channelName) ? null : channelName,
            Release = episode.ReleaseUtc == default ? null : episode.ReleaseUtc,
            Length = episode.Length == default ? null : episode.Length,
            SourceEpisodeId = episode.Id,
            AddedAt = addedAtUtc
        };
    }

    /// <summary>
    /// An alternate from a YouTube URL alone (Edit Film paste). The url is stored as
    /// <c>https://www.youtube.com/watch?v={id}</c>. Null when the url has no video id.
    /// </summary>
    public static YouTubeAlternate? FromUrl(Uri url, DateTime addedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(url);
        var youTubeId = YouTubeIdResolver.Extract(url);
        if (string.IsNullOrWhiteSpace(youTubeId))
        {
            return null;
        }

        return new YouTubeAlternate
        {
            YouTubeId = youTubeId,
            Url = new Uri($"https://www.youtube.com/watch?v={youTubeId}"),
            AddedAt = addedAtUtc
        };
    }

    /// <summary>
    /// The primary YouTube link of <paramref name="item"/>, as an alternate, so a promote can park it
    /// on the list. Null when the item has no primary YouTube url or id.
    /// </summary>
    public static YouTubeAlternate? FromPrimary(
        IReadOnlyDictionary<string, ServiceLink>? services,
        string? primaryYouTubeId,
        DateTime addedAtUtc)
    {
        ServiceLink? link = null;
        services?.TryGetValue(ServiceKeys.YouTube, out link);
        var url = link?.Url;
        var youTubeId = string.IsNullOrWhiteSpace(primaryYouTubeId)
            ? url is null ? null : YouTubeIdResolver.Extract(url)
            : primaryYouTubeId;
        if (string.IsNullOrWhiteSpace(youTubeId) || url is null)
        {
            return null;
        }

        return new YouTubeAlternate
        {
            YouTubeId = youTubeId,
            Url = url,
            Image = link?.Image,
            AddedAt = addedAtUtc
        };
    }
}
