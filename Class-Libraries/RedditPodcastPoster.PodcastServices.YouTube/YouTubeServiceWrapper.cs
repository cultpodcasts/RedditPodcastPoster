using Google.Apis.YouTube.v3;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public record YouTubeServiceWrapper(YouTubeService YouTubeService, string ApiKeyName);