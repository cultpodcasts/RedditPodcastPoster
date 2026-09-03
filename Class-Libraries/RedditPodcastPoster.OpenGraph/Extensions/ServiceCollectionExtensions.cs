using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.OpenGraph.Extractors;

namespace RedditPodcastPoster.OpenGraph.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenGraphExtractor(this IServiceCollection services)
    {
        return services.AddScoped<OpenGraphPageMetaDataExtractor>();
    }
}
