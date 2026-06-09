using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public static class PlaylistItemOrdering
{
    public static bool IsReverseDateOrdered(IEnumerable<PlaylistItem> source)
    {
        using var iterator = source.GetEnumerator();
        if (!iterator.MoveNext())
        {
            return true;
        }

        var current = iterator.Current.Snippet.PublishedAtDateTimeOffset;

        while (iterator.MoveNext())
        {
            var next = iterator.Current.Snippet.PublishedAtDateTimeOffset;
            if (current < next)
            {
                return false;
            }

            current = next;
        }

        return true;
    }
}
