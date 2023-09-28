namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public record GetLatestYouTubeChannelVideosRequest(string YouTubeChannelId, DateTime? PublishedSince);