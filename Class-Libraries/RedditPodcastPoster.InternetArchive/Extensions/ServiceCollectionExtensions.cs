using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.InternetArchive.Clients;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.InternetArchive.Factories;
using RedditPodcastPoster.InternetArchive.JsonConverters;
using RedditPodcastPoster.InternetArchive.Matching;
using RedditPodcastPoster.InternetArchive.Models;
using RedditPodcastPoster.InternetArchive.Providers;

namespace RedditPodcastPoster.InternetArchive.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInternetArchiveServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IInternetArchivePlayListProvider, InternetArchivePlayListProvider>()
            .AddSingleton<IInternetArchiveHttpClientFactory, InternetArchiveHttpClientFactory>()
            .AddScoped(s => s.GetService<IInternetArchiveHttpClientFactory>()!.Create())
            .AddScoped<IInternetArchivePageMetaDataExtractor, InternetArchivePageMetaDataExtractor>()
            .AddScoped<IMetaDataExtractor, MetaDataExtractor>();
    }
}
