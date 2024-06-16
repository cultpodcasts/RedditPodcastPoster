using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Taddy.Configuration;

namespace RedditPodcastPoster.PodcastServices.Taddy.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTaddy(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.BindConfiguration<TaddyOptions>("taddy");

        return services
            .AddScoped<ITaddySearcher, TaddySearcher>()
            .AddSingleton<ITaddyClientFactory, TaddyClientFactory>()
            .AddScoped(s => s.GetService<ITaddyClientFactory>()!.Create());
    }
}