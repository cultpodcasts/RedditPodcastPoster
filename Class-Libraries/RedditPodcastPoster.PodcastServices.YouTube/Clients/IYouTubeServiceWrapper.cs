using Google.Apis.YouTube.v3;

namespace RedditPodcastPoster.PodcastServices.YouTube.Clients;

public interface IYouTubeServiceWrapper
{
    YouTubeService YouTubeService { get; }
    bool CanRotate { get; }
    void Rotate();
}