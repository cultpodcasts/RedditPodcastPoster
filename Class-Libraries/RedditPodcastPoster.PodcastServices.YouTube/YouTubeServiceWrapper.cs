using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeServiceWrapper(
    YouTubeService youTubeService,
    ApplicationWrapper applicationWrapper,
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy) : IYouTubeServiceWrapper
{
    private ApplicationWrapper _applicationWrapper = applicationWrapper;
    private int _reattempt;
    public YouTubeService YouTubeService { get; private set; } = youTubeService;
    public ApplicationUsage ApplicationUsage => _applicationWrapper.Application.Usage;
    public int Reattempts => _applicationWrapper.Reattempts;
    public int Index => _applicationWrapper.Index;

    public void Rotate()
    {
        var application = youTubeApiKeyStrategy.GetApplication(ApplicationUsage, Index, ++_reattempt);
        YouTubeService =
            new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = application.Application.ApiKey,
                ApplicationName = application.Application.Name
            });
        _applicationWrapper = application;
    }
}