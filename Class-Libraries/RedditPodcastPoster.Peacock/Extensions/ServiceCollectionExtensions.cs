using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Peacock.Extractors;
using RedditPodcastPoster.Peacock.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Peacock.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPeacockServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(PeacockPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IPeacockPageMetaDataExtractor, PeacockPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
            {
                var extractor = provider.GetRequiredService<IPeacockPageMetaDataExtractor>();
                return new CatalogKeyedNonPodcastServiceAdapter(
                    StreamingService.Peacock,
                    PeacockUrlMatcher.IsSubmitUrl,
                    PeacockUrlMatcher.IsSubmitUrl,
                    extractor.GetMetaData,
                    extractor.ExtractFromHtml);
            });
    }
}
