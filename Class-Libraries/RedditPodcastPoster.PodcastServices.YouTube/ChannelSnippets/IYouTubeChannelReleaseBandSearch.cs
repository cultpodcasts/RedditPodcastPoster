using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public interface IYouTubeChannelReleaseBandSearch
{
    /// <summary>
    /// Videos on one channel whose publish time falls inside the band. Does not page through to now.
    /// </summary>
    Task<IList<SearchResult>?> Search(
        string channelId,
        DateTimeOffset publishedAfter,
        DateTimeOffset publishedBefore,
        IndexingContext indexingContext);
}
