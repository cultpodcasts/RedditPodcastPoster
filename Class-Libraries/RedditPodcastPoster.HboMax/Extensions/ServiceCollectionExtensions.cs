using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.HboMax.Extractors;
using RedditPodcastPoster.HboMax.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.HboMax.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHboMaxServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(HboMaxPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IHboMaxPageMetaDataExtractor, HboMaxPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.HboMax,
                    ServiceKeys.HboMax,
                    HboMaxUrlMatcher.IsSubmitUrl,
                    HboMaxUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IHboMaxPageMetaDataExtractor>().GetMetaData));
    }
}
