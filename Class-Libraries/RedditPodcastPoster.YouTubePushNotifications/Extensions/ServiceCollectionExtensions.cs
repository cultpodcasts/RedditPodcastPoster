using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.YouTubePushNotifications.Configuration;

namespace RedditPodcastPoster.YouTubePushNotifications.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYouTubePushNotificationServices(this IServiceCollection services,
        IConfiguration config)
    {
        services.AddOptions<YouTubePushNotificationCallbackSettings>()
            .Bind(config.GetSection("youTubePushNotification"));

        return services
            .AddScoped<IPodcastsSubscriber, PodcastsSubscriber>()
            .AddScoped<IPodcastYouTubePushNotificationSubscriber, PodcastYouTubePushNotificationSubscriber>();
    }
}