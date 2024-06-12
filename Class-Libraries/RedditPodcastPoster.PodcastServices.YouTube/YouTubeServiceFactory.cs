using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeServiceFactory(
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<YouTubeServiceFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IYouTubeServiceFactory
{
    public YouTubeService Create()
    {
        var application = youTubeApiKeyStrategy.GetApplication();
        return new YouTubeService(new BaseClientService.Initializer
        {
            ApiKey = application.ApiKey,
            ApplicationName = application.Name
        });
    }

    public static IServiceCollection AddYouTubeService(IServiceCollection services)
    {
        return services
            .AddScoped<IYouTubeServiceFactory, YouTubeServiceFactory>()
            .AddScoped<IYouTubeApiKeyStrategy, YouTubeApiKeyStrategy>()
            .AddDateTimeService()
            .AddScoped(s => s.GetService<IYouTubeServiceFactory>()!.Create());
    }
}