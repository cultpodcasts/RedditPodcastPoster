using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.BBC.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBBCServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IBBCHttpClientFactory, BBCHttpClientFactory>()
            .AddScoped(s => s.GetService<IBBCHttpClientFactory>()!.Create())
            .AddScoped<IiPlayerPageMetaDataExtractor, iPlayerPageMetaDataExtractor>()
            .AddScoped<IMetaDataExtractor, MetaDataExtractor>();
    }
}