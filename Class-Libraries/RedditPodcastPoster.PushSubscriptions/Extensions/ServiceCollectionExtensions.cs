﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PushSubscriptions.Configuration;

namespace RedditPodcastPoster.PushSubscriptions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPushSubscriptionsRepository(this IServiceCollection services,
        IConfiguration config)
    {
        return services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
    }

    public static IServiceCollection AddPushSubscriptions(this IServiceCollection services, IConfiguration config)
    {
        services.BindConfiguration<PushSubscriptionsOptions>("pushSubscriptions");
        return services
            .AddPushSubscriptionsRepository(config)
            .AddScoped<INotificationPublisher, NotificationPublisher>();
    }
}