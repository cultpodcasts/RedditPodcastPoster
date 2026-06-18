using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Clients;

public class YouTubeServiceWrapper(
    YouTubeService youTubeService,
    ApplicationWrapper applicationWrapper,
    ApplicationUsage usage,
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy,
    IReadOnlyList<ApplicationWrapper>? indexerKeyRing,
    int initialRingIndex,
    IYouTubeIndexerKeyStateService? indexerKeyStateService,
    ILogger<YouTubeServiceWrapper> logger
) : IYouTubeServiceWrapper, IAsyncDisposable
{
    private ApplicationWrapper _applicationWrapper = applicationWrapper;
    private int _reattempt;
    private int _ringIndex = initialRingIndex;
    private bool _sessionPersisted;
    private readonly IReadOnlyList<ApplicationWrapper> _indexerKeyRing =
        usage == ApplicationUsage.Indexer
            ? indexerKeyRing ?? youTubeApiKeyStrategy.BuildIndexerKeyRing(applicationWrapper.Index)
            : [];

    public YouTubeService YouTubeService { get; private set; } = youTubeService;
    public ApplicationUsage Usage { get; } = usage;
    public Application CurrentApplication => _applicationWrapper.Application;
    internal int IndexerRingIndex => _ringIndex;

    public bool CanRotate =>
        Usage == ApplicationUsage.Indexer
            ? _ringIndex < _indexerKeyRing.Count
            : _reattempt <= _applicationWrapper.Reattempts;

    public void Rotate()
    {
        if (Usage == ApplicationUsage.Indexer)
        {
            RotateIndexerKey();
            return;
        }

        logger.LogInformation("Rotate api-key from {apiKey}. usage: '{usage}', index: {index}, reattempt: {reattempt}",
            _applicationWrapper.Application.DisplayName,
            _applicationWrapper.Application.Usage,
            _applicationWrapper.Index,
            _reattempt + 1);
        var application = youTubeApiKeyStrategy.GetApplication(
            _applicationWrapper.Application.Usage,
            _applicationWrapper.Index,
            ++_reattempt);
        ApplyApplication(application);
    }

    private void RotateIndexerKey()
    {
        if (_ringIndex >= _indexerKeyRing.Count - 1)
        {
            throw new InvalidOperationException("Indexer key ring exhausted.");
        }

        logger.LogInformation(
            "Rotate indexer api-key from {apiKey} ({ringIndex}/{ringCount}).",
            _applicationWrapper.Application.DisplayName,
            _ringIndex + 1,
            _indexerKeyRing.Count);
        _ringIndex++;
        ApplyApplication(_indexerKeyRing[_ringIndex]);
    }

    private void ApplyApplication(ApplicationWrapper application)
    {
        logger.LogInformation("Obtained api-key '{apiKey}'.", application.Application.DisplayName);
        YouTubeService =
            new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = application.Application.ApiKey,
                ApplicationName = application.Application.Name
            });
        _applicationWrapper = application;
    }

    public async ValueTask DisposeAsync()
    {
        if (Usage != ApplicationUsage.Indexer || indexerKeyStateService == null || _sessionPersisted)
        {
            return;
        }

        _sessionPersisted = true;
        await indexerKeyStateService.PersistSessionEndAsync(_ringIndex, CurrentApplication.ApiKey);
    }
}
