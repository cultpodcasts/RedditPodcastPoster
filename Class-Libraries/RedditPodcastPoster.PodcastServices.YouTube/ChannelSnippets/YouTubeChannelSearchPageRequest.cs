namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

/// <summary>
/// One <c>search.list</c> page for a channel, bounded by publish time.
/// </summary>
public sealed record YouTubeChannelSearchPageRequest(
    string ChannelId,
    DateTimeOffset PublishedAfterDateTimeOffset,
    DateTimeOffset PublishedBeforeDateTimeOffset,
    string PageToken,
    int MaxResults);
