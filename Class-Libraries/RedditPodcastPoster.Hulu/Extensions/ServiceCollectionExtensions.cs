using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Hulu.Extractors;
using RedditPodcastPoster.Hulu.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Hulu.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHuluServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(HuluPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IHuluPageMetaDataExtractor, HuluPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
            {
                var extractor = provider.GetRequiredService<IHuluPageMetaDataExtractor>();
                return new CatalogKeyedNonPodcastServiceAdapter(
                    StreamingService.Hulu,
                    HuluUrlMatcher.IsSubmitUrl,
                    HuluUrlMatcher.IsSubmitUrl,
                    extractor.GetMetaData,
                    extractor.ExtractFromHtml);
            });
    }
}
