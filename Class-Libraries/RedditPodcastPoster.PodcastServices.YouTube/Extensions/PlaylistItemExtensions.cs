using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class PlaylistItemExtensions
{
    public const int MaxMatchCandidatesWithoutReleasedSince = 5;

    public static string GetVideoId(this PlaylistItem item) =>
        item.ContentDetails?.VideoId ?? item.Snippet.ResourceId.VideoId;

    /// <summary>
    /// The date a playlist item enters an <see cref="IndexingContext.ReleasedSince"/> window.
    /// A playlist item's <c>snippet.publishedAt</c> is the added-to-playlist time, which a scheduled
    /// upload receives days before the video becomes public, so take whichever of added-at and the
    /// video's own publication is later. Backlog videos added to a curated playlist long after they
    /// were published keep their added-at, which is the "new to this feed" signal.
    /// </summary>
    public static DateTimeOffset? GetIndexingWindowDate(this PlaylistItem item) =>
        LaterOf(item.Snippet?.PublishedAtDateTimeOffset, item.ContentDetails?.VideoPublishedAtDateTimeOffset);

    /// <inheritdoc cref="GetIndexingWindowDate(PlaylistItem)"/>
    public static DateTimeOffset? GetIndexingWindowDate(
        this PlaylistItemSnippet playlistItemSnippet,
        Google.Apis.YouTube.v3.Data.Video? videoDetails) =>
        LaterOf(
            playlistItemSnippet.PublishedAtDateTimeOffset,
            videoDetails?.Snippet?.PublishedAtDateTimeOffset);

    public static IList<PlaylistItem> ForEpisodeMatching(
        this IEnumerable<PlaylistItem> items,
        IndexingContext indexingContext)
    {
        if (indexingContext.ReleasedSince.HasValue)
        {
            return items
                .Where(x => x.GetIndexingWindowDate().ReleasedSinceDate(indexingContext.ReleasedSince))
                .ToList();
        }

        return items.Take(MaxMatchCandidatesWithoutReleasedSince).ToList();
    }

    private static DateTimeOffset? LaterOf(DateTimeOffset? first, DateTimeOffset? second)
    {
        if (first == null)
        {
            return second;
        }

        if (second == null)
        {
            return first;
        }

        return second > first ? second : first;
    }
}
