using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.BBC.Clients;
using RedditPodcastPoster.BBC.DTOs;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.BBC.Factories;
using RedditPodcastPoster.BBC.Matching;

namespace RedditPodcastPoster.BBC.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBBCServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IBBCHttpClientFactory, BBCHttpClientFactory>()
            .AddScoped(s => s.GetService<IBBCHttpClientFactory>()!.Create())
            .AddScoped<IBBCPageMetaDataExtractor, BBCPageMetaDataExtractor>()
            .AddScoped<IiPlayerPageMetaDataExtractor, iPlayerPageMetaDataExtractor>()
            .AddScoped< ISoundsPageMetaDataExtractor, SoundsPageMetaDataExtractor>();
    }
}
