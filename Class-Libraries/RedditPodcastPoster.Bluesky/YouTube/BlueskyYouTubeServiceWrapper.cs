using Google.Apis.YouTube.v3;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.Bluesky.YouTube;

public class BlueskyYouTubeServiceWrapper(IYouTubeServiceWrapper applicationWrapper) : IBlueskyYouTubeServiceWrapper
{
    public YouTubeService YouTubeService { get; } = applicationWrapper.YouTubeService;
    public ApplicationUsage Usage { get; } = applicationWrapper.Usage;
    public Application CurrentApplication { get; } = applicationWrapper.CurrentApplication;
    public bool CanRotate => applicationWrapper.CanRotate;

    public void Rotate()
    {
        applicationWrapper.Rotate();
    }
}
