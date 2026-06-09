namespace RedditPodcastPoster.PodcastServices.YouTube.Exceptions;

public class YouTubeChannelSearchForbiddenException(string channelId, Exception innerException)
    : Exception($"Search.List is not permitted for channel-id '{channelId}'.", innerException)
{
    public string ChannelId { get; } = channelId;
}
