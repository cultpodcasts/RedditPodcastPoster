using Google.Apis.YouTube.v3;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeServiceWrapper
{
    YouTubeService YouTubeService { get; }
    ApplicationUsage ApplicationUsage { get; }
    int Reattempts { get; }
    int Index { get; }
    void Rotate();
}