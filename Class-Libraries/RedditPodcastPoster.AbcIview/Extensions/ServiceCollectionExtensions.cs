using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.AbcIview.Extractors;
using RedditPodcastPoster.AbcIview.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.AbcIview.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAbcIviewServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(AbcIviewPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IAbcIviewPageMetaDataExtractor, AbcIviewPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    StreamingService.AbcIview,
                    AbcIviewUrlMatcher.IsSubmitUrl,
                    AbcIviewUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IAbcIviewPageMetaDataExtractor>().GetMetaData));
    }
}