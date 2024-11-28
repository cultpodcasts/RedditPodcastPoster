using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeServiceWrapper(
    YouTubeService youTubeService,
    ApplicationWrapper applicationWrapper,
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy,
    ILogger<YouTubeServiceWrapper> logger
) : IYouTubeServiceWrapper
{
    private ApplicationWrapper _applicationWrapper = applicationWrapper;
    private int _reattempt;
    public YouTubeService YouTubeService { get; private set; } = youTubeService;
    public bool CanRotate => _reattempt < _applicationWrapper.Reattempts;

    public void Rotate()
    {
        logger.LogInformation("Rotate api-key from {apiKey}. usage: '{usage}', index: {index}, reattempt: {reattempt}",
            _applicationWrapper.Application.DisplayName,
            _applicationWrapper.Application.Usage,
            _applicationWrapper.Index,
            _reattempt + 1);
        var application = youTubeApiKeyStrategy.GetApplication(
            _applicationWrapper.Application.Usage,
            _applicationWrapper.Index,
            ++_reattempt);
        logger.LogInformation("Obtained api-key '{apiKey}'.", application.Application.DisplayName);
        YouTubeService =
            new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = application.Application.ApiKey,
                ApplicationName = application.Application.Name
            });
        _applicationWrapper = application;
    }
}