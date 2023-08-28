using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeServiceFactory : IYouTubeServiceFactory
{
    private readonly ILogger<YouTubeServiceFactory> _logger;
    private readonly YouTubeSettings _settings;

    public YouTubeServiceFactory(IOptions<YouTubeSettings> settings, ILogger<YouTubeServiceFactory> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

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