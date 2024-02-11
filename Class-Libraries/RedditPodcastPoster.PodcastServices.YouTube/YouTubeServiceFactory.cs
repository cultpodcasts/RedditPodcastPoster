using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeServiceFactory(
    IOptions<YouTubeSettings> settings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<YouTubeServiceFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IYouTubeServiceFactory
{
    private readonly YouTubeSettings _settings = settings.Value;

    public YouTubeService Create()
    {
        return new YouTubeService(new BaseClientService.Initializer
        {
            ApiKey = _settings.ApiKey,
            ApplicationName = "CultPodcasts"
        });
    }

    public static IServiceCollection AddYouTubeService(IServiceCollection services)
    {
        return services
            .AddScoped<IYouTubeServiceFactory, YouTubeServiceFactory>()
            .AddScoped(s => s.GetService<IYouTubeServiceFactory>()!.Create());
    }
}