using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.InternetArchive.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInternetArchiveServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IInternetArchiveHttpClientFactory, InternetArchiveHttpClientFactory>()
            .AddScoped(s => s.GetService<IInternetArchiveHttpClientFactory>()!.Create())
            .AddScoped<IInternetArchivePageMetaDataExtractor, InternetArchivePageMetaDataExtractor>()
            .AddScoped<IMetaDataExtractor, MetaDataExtractor>();
    }
}