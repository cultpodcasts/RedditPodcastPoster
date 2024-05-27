using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Configuration;

namespace RedditPodcastPoster.UrlShortening.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShortnerServices(this IServiceCollection services, IConfiguration config)
    {
        services.BindConfiguration<CloudFlareOptions>("cloudflare");
        services.AddScoped<IShortnerService, ShortnerService>();
        return services;
    }
}