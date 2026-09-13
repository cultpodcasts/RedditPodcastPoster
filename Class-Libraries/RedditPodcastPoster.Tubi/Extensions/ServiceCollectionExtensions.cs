using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.Tubi.Extractors;
using RedditPodcastPoster.Tubi.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Tubi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTubiServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(TubiPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<ITubiPageMetaDataExtractor, TubiPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
            {
                var extractor = provider.GetRequiredService<ITubiPageMetaDataExtractor>();
                return new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.Tubi,
                    StreamingServiceKeys.Tubi,
                    TubiUrlMatcher.IsSubmitUrl,
                    TubiUrlMatcher.IsSubmitUrl,
                    extractor.GetMetaData,
                    extractor.ExtractFromHtml,
                    TubiUrlMatcher.CanonicalUrl);
            });
    }
}
