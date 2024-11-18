using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Bluesky.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlueskyServices(this IServiceCollection services, IConfiguration config)
    {
        services.BindConfiguration<BlueskyOptions>("bluesky");

        return services
            .AddSingleton<IBlueskyClientFactory, BlueskyClientFactory>()
            .AddSingleton(x => x.GetService<IBlueskyClientFactory>()!.Create())
            .AddScoped<IBlueskyPostBuilder, BlueskyPostBuilder>()
            .AddScoped<IBlueskyPoster, BlueskyPoster>()
            .AddScoped<IBlueskyPostManager, BlueskyPostManager>();
    }
}