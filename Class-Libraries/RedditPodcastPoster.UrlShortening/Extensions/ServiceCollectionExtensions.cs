using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.UrlShortening.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShortnerServices(this IServiceCollection services, IConfiguration config)
    {
        services.BindConfiguration<CloudFlareOptions>("cloudflare");
        services.BindConfiguration<ShortnerOptions>("shortner");
        services.AddScoped<IShortnerService, ShortnerService>();
        return services;
    }
}