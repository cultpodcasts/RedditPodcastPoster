using Google.Apis.YouTube.v3;

namespace RedditPodcastPoster.PodcastServices.YouTube;


public interface IYouTubeServiceWrapper
{
    YouTubeService YouTubeService { get; }
    bool CanRotate { get; }
    void Rotate();
}