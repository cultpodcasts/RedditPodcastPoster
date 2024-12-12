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

        return services
            .AddSingleton<IBlueskyClientFactory, BlueskyClientFactory>()
            .AddSingleton(x => x.GetService<IBlueskyClientFactory>()!.Create())
            .AddScoped<IBlueskyEmbedCardPostFactory, BlueskyEmbedCardPostFactory>()
            .AddScoped<IBlueskyPoster, BlueskyPoster>()
            .AddScoped<IBlueskyPostManager, BlueskyPostManager>()
            .AddScoped<IEmbedCardRequestFactory, EmbedCardRequestFactory>()
            .AddScoped<IBlueskyYouTubeServiceFactory, BlueskyYouTubeServiceFactory>()
            .AddScoped(s => s.GetService<IBlueskyYouTubeServiceFactory>()!.Create())
            .BindConfiguration<BlueskyOptions>("bluesky");
    }
}