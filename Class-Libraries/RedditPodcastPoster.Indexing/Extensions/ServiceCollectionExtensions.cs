using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.Indexing.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndexer(this IServiceCollection services)
    {
        return services.AddScoped<IIndexer, Indexer>();
    }
}