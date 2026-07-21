using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.YouTubePushNotifications.Adaptors;
using RedditPodcastPoster.YouTubePushNotifications.Configuration;
using RedditPodcastPoster.YouTubePushNotifications.Extensions;
using RedditPodcastPoster.YouTubePushNotifications.Handlers;
using RedditPodcastPoster.YouTubePushNotifications.Models;
using RedditPodcastPoster.YouTubePushNotifications.Subscribers;

namespace RedditPodcastPoster.YouTubePushNotifications.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYouTubePushNotificationServices(
        this IServiceCollection services)
    {
        return services
            .AddScoped<IPodcastsSubscriber, PodcastsSubscriber>()
            .AddScoped<IPodcastYouTubePushNotificationSubscriber, PodcastYouTubePushNotificationSubscriber>()
            .AddScoped<INotificationAdaptor, NotificationAdaptor>()
            .AddScoped<IPushNotificationHandler, PushNotificationHandler>()
            .AddSingleton<IPodcastYouTubePushNotificationUrlAdaptor, PodcastYouTubePushNotificationUrlAdaptor>()
            .BindConfiguration<YouTubePushNotificationCallbackSettings>("youTubePushNotification");
    }
}
