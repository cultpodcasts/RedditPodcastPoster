using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Fawesome.Extractors;
using RedditPodcastPoster.Fawesome.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Fawesome.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFawesomeServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(FawesomePageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IFawesomePageMetaDataExtractor, FawesomePageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.Fawesome,
                    StreamingServiceKeys.Fawesome,
                    FawesomeUrlMatcher.IsSubmitUrl,
                    FawesomeUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IFawesomePageMetaDataExtractor>().GetMetaData));
    }
}
