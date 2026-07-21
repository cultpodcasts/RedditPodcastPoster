using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.UrlShortening.Configuration;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Models;
using RedditPodcastPoster.UrlShortening.Services;

namespace RedditPodcastPoster.UrlShortening.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShortnerServices(this IServiceCollection services)
    {
        return services
            .BindConfiguration<ShortnerOptions>("shortner")
            .AddScoped<IShortnerService, ShortnerService>();
    }
}
