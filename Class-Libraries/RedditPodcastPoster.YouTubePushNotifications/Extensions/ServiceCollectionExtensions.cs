using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.YouTubePushNotifications.Configuration;

namespace RedditPodcastPoster.YouTubePushNotifications.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYouTubePushNotificationServices(this IServiceCollection services,
        IConfiguration config)
    {
        services.BindConfiguration<YouTubePushNotificationCallbackSettings>("youTubePushNotification");

        return services
            .AddScoped<IPodcastsSubscriber, PodcastsSubscriber>()
            .AddScoped<IPodcastYouTubePushNotificationSubscriber, PodcastYouTubePushNotificationSubscriber>()
            .AddScoped<INotificationAdaptor, NotificationAdaptor>()
            .AddScoped<IPushNotificationHandler, PushNotificationHandler>()
            .AddSingleton<IPodcastYouTubePushNotificationUrlAdaptor, PodcastYouTubePushNotificationUrlAdaptor>();
    }
}