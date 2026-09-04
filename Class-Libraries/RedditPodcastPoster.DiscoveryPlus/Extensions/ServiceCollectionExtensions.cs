using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.DiscoveryPlus.Extractors;
using RedditPodcastPoster.DiscoveryPlus.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.DiscoveryPlus.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscoveryPlusServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(DiscoveryPlusPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IDiscoveryPlusPageMetaDataExtractor, DiscoveryPlusPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.DiscoveryPlus,
                    ServiceKeys.DiscoveryPlus,
                    DiscoveryPlusUrlMatcher.IsSubmitUrl,
                    DiscoveryPlusUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IDiscoveryPlusPageMetaDataExtractor>().GetMetaData));
    }
}
