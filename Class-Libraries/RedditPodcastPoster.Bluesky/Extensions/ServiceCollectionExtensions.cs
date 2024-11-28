using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.YouTube;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Bluesky.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlueskyServices(this IServiceCollection services)
    {
        services.BindConfiguration<BlueskyOptions>("bluesky");

        return services
            .AddSingleton<IBlueskyClientFactory, BlueskyClientFactory>()
            .AddSingleton(x => x.GetService<IBlueskyClientFactory>()!.Create())
            .AddScoped<IBlueskyPostBuilder, BlueskyPostBuilder>()
            .AddScoped<IBlueskyPoster, BlueskyPoster>()
            .AddScoped<IBlueskyPostManager, BlueskyPostManager>()
            .AddScoped<IEmbedCardRequestFactory, EmbedCardRequestFactory>()
            .AddScoped<IBlueskyYouTubeServiceFactory, BlueskyYouTubeServiceFactory>()
            .AddScoped<IBlueskyYouTubeServiceWrapper>(s => s.GetService<IBlueskyYouTubeServiceFactory>()!.Create());
    }
}