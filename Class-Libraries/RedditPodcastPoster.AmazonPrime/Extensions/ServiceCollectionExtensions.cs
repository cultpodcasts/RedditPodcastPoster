using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.AmazonPrime.Extractors;
using RedditPodcastPoster.AmazonPrime.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.AmazonPrime.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAmazonPrimeServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(AmazonPrimePageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IAmazonPrimePageMetaDataExtractor, AmazonPrimePageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.AmazonPrime,
                    ServiceKeys.AmazonPrime,
                    AmazonPrimeUrlMatcher.IsSubmitUrl,
                    AmazonPrimeUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IAmazonPrimePageMetaDataExtractor>().GetMetaData));
    }
}
