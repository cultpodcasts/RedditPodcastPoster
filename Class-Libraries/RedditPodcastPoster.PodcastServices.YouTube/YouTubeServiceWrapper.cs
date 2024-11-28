using Google.Apis.YouTube.v3;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public record YouTubeServiceWrapper(
    YouTubeService YouTubeService,
    string ApiKeyName,
    ApplicationUsage Usage,
    int Index,
    int Reattempts);