using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeServiceFactory(
    IApplicationUsageProvider applicationUsageProvider,
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<YouTubeServiceFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IYouTubeServiceFactory
{
    public IYouTubeServiceWrapper Create()
    {
        return Create(applicationUsageProvider.GetApplicationUsage());
    }

    public IYouTubeServiceWrapper Create(ApplicationUsage usage)
    {
        var application = youTubeApiKeyStrategy.GetApplication(usage);
        return new YouTubeServiceWrapper(
            new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = application.Application.ApiKey,
                ApplicationName = application.Application.Name
            }),
            application,
            youTubeApiKeyStrategy);
    }
}