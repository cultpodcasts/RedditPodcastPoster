using Google.Apis.YouTube.v3;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;

namespace RedditPodcastPoster.Bluesky.YouTube;

public class BlueskyYouTubeServiceWrapper(IYouTubeServiceWrapper applicationWrapper) : IBlueskyYouTubeServiceWrapper
{
    public YouTubeService YouTubeService { get; } = applicationWrapper.YouTubeService;
    public bool CanRotate => applicationWrapper.CanRotate;

    public void Rotate()
    {
        applicationWrapper.Rotate();
    }
}