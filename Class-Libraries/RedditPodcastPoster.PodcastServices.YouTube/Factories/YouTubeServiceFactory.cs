using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Factories;

public class YouTubeServiceFactory(
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy,
    IServiceProvider serviceProvider,
    ILogger<YouTubeServiceFactory> logger,
    ILogger<YouTubeServiceWrapper> injectedLogger
) : IYouTubeServiceFactory
{
    public IYouTubeServiceWrapper Create(ApplicationUsage usage)
    {
        logger.LogInformation("Create youtube-service for usage '{usage}'.", usage);

        IndexerKeyRingSessionStart? session = null;
        IYouTubeIndexerKeyStateService? indexerKeyStateService = null;
        if (usage == ApplicationUsage.Indexer)
        {
            var sessionBootstrapper = serviceProvider.GetService<YouTubeIndexerKeySessionBootstrapper>();
            sessionBootstrapper?.EnsureLoadedAsync().GetAwaiter().GetResult();
            session = serviceProvider.GetService<YouTubeIndexerKeyRingSessionHolder>()?.Value;
            indexerKeyStateService = serviceProvider.GetService<IYouTubeIndexerKeyStateService>();
        }

        IReadOnlyList<ApplicationWrapper>? indexerKeyRing = usage == ApplicationUsage.Indexer
            ? session?.Ring ?? youTubeApiKeyStrategy.BuildIndexerKeyRing(0)
            : null;
        var initialRingIndex = session?.InitialRingIndex
            ?? (usage == ApplicationUsage.Indexer
                ? youTubeApiKeyStrategy.GetApplication(usage).Index
                : 0);
        var application = session?.Ring[initialRingIndex]
            ?? indexerKeyRing![initialRingIndex];

        logger.LogInformation(
            "Obtained api-key: '{apiKey}' at ring index {RingIndex}.",
            application.Application.DisplayName,
            initialRingIndex);

        return new YouTubeServiceWrapper(
            new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = application.Application.ApiKey,
                ApplicationName = application.Application.Name
            }),
            application,
            usage,
            youTubeApiKeyStrategy,
            indexerKeyRing,
            initialRingIndex,
            indexerKeyStateService,
            injectedLogger);
    }
}
