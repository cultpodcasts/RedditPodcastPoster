using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeServiceFactory(
    IApplicationUsageProvider applicationUsageProvider,
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy,
    ILogger<YouTubeServiceFactory> logger,
    ILogger<YouTubeServiceWrapper> injectedLogger
) : IYouTubeServiceFactory
{
    public IYouTubeServiceWrapper Create()
    {
        logger.LogInformation("Create youtube-service for default-usage.");
        return Create(applicationUsageProvider.GetApplicationUsage());
    }

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