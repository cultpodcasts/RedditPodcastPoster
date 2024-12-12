using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

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