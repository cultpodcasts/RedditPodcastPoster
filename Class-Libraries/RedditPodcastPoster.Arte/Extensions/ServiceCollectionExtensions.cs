using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Arte.Extractors;
using RedditPodcastPoster.Arte.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Arte.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArteServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(ArtePageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IArtePageMetaDataExtractor, ArtePageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    StreamingService.Arte,
                    ArteUrlMatcher.IsSubmitUrl,
                    ArteUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IArtePageMetaDataExtractor>().GetMetaData));
    }
}