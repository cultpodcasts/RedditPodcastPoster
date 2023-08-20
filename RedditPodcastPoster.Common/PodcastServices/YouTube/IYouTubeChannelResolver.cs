namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeChannelResolver
{
    Task<string?> FindChannel(string channelName, string mostRecentlyUploadVideoTitle);
}