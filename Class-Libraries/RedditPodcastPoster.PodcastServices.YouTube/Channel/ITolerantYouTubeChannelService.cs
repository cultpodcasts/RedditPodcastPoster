using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Channel;

public interface ITolerantYouTubeChannelService
{
    Task<Google.Apis.YouTube.v3.Data.Channel?> GetChannel(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false,
        bool withStatistics = false,
        bool withContentDetails = false);

    /// <summary>As <see cref="GetChannel"/>, but says why no channel was returned (skipped, not found, API error, quota). A pass-cache hit is Found even when YouTube is bypassed.</summary>
    Task<YouTubeChannelLookupResult> GetChannelLookup(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool withSnippets = false,
        bool withContentOwnerDetails = false,
        bool withStatistics = false,
        bool withContentDetails = false);
}
