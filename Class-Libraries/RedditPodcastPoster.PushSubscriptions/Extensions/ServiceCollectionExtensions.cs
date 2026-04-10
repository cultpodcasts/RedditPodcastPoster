using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PushSubscriptions.Configuration;

namespace RedditPodcastPoster.PushSubscriptions.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPushSubscriptionsRepository()
        {
            return services
                .AddSingleton<IPushSubscriptionRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PushSubscriptionRepository>>();
                    return new PushSubscriptionRepository(containerFactory.CreatePushSubscriptionsContainer(), logger);
                })
                ;
        }

        public IServiceCollection AddPushSubscriptions()
        {
            return services
                .AddPushSubscriptionsRepository()
                .AddScoped<INotificationPublisher, NotificationPublisher>()
                .BindConfiguration<PushSubscriptionsOptions>("pushSubscriptions");
        }
    }
}