using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube.Factories;

public class YouTubeVideoServiceFactory(
    IYouTubeServiceFactory youTubeServiceFactory,
    ILogger<YouTubeVideoServiceFactory> logger,
    ILogger<YouTubeVideoService> youTubeVideoServiceLogger
) : IYouTubeVideoServiceFactory
{
    public YouTubeVideoService Create(ApplicationUsage applicationUsage)
    {
        logger.LogInformation("Creating youtube-video-service for usage {applicationUsage}",
            applicationUsage.ToString());
        var youTubeServiceWrapper = youTubeServiceFactory.Create(applicationUsage);
        logger.LogInformation("Obtained youtube-video-service with api-key {apiKeyName}",
            youTubeServiceWrapper.ApiKeyName);
        return new YouTubeVideoService(youTubeServiceWrapper, youTubeVideoServiceLogger);
    }
}