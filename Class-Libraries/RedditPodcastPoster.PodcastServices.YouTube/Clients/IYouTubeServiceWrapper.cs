using Google.Apis.YouTube.v3;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube.Clients;

public interface IYouTubeServiceWrapper
{
    YouTubeService YouTubeService { get; }
    ApplicationUsage Usage { get; }
    Application CurrentApplication { get; }
    bool CanRotate { get; }
    void Rotate();
}
