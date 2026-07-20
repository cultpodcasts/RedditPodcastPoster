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

    public static IList<PlaylistItem> ForEpisodeMatching(
        this IEnumerable<PlaylistItem> items,
        IndexingContext indexingContext)
    {
        if (indexingContext.ReleasedSince.HasValue)
        {
            return items
                .Where(x => x.Snippet.PublishedAtDateTimeOffset.ReleasedSinceDate(indexingContext.ReleasedSince))
                .ToList();
        }

        return items.Take(MaxMatchCandidatesWithoutReleasedSince).ToList();
    }
}
