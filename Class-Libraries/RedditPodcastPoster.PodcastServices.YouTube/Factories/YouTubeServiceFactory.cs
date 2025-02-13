﻿using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Factories;

public class YouTubeServiceFactory(
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy,
    ILogger<YouTubeServiceFactory> logger,
    ILogger<YouTubeServiceWrapper> injectedLogger
) : IYouTubeServiceFactory
{
    public IYouTubeServiceWrapper Create(ApplicationUsage usage)
    {
        logger.LogInformation("Create youtube-service for usage '{usage}'.", usage);
        var application = youTubeApiKeyStrategy.GetApplication(usage);
        logger.LogInformation("Obtained api-key: '{apiKey}'", application.Application.DisplayName);
        return new YouTubeServiceWrapper(
            new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = application.Application.ApiKey,
                ApplicationName = application.Application.Name
            }),
            application,
            youTubeApiKeyStrategy,
            injectedLogger);
    }
}