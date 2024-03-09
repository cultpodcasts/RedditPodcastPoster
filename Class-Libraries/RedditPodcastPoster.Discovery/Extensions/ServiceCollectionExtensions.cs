using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.Discovery.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscovery(
        this IServiceCollection services,
        ConfigurationManager config)
    {
        return services
            .AddScoped<ISearchProvider, SearchProvider>();
    }
}