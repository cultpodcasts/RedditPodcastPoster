using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeServiceFactory(
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<YouTubeServiceFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IYouTubeServiceFactory
{
    public YouTubeServiceWrapper Create(ApplicationUsage usage)
    {
        var application = youTubeApiKeyStrategy.GetApplication(usage);
        return new YouTubeServiceWrapper(
            new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = application.ApiKey,
                ApplicationName = application.Name
            }), application.DisplayName);
    }

    public static IServiceCollection AddYouTubeService(IServiceCollection services, ApplicationUsage usage)
    {
        return services
            .AddScoped<IYouTubeServiceFactory, YouTubeServiceFactory>()
            .AddScoped<IYouTubeApiKeyStrategy, YouTubeApiKeyStrategy>()
            .AddDateTimeService()
            .AddScoped(s => s.GetService<IYouTubeServiceFactory>()!.Create(usage));
    }
}