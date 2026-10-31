using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.CbcGem.Extractors;
using RedditPodcastPoster.CbcGem.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.CbcGem.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCbcGemServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(CbcGemPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddScoped<ICbcGemPageMetaDataExtractor, CbcGemPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
            {
                var extractor = provider.GetRequiredService<ICbcGemPageMetaDataExtractor>();
                return new CatalogKeyedNonPodcastServiceAdapter(
                    StreamingService.CbcGem,
                    CbcGemUrlMatcher.IsSubmitUrl,
                    CbcGemUrlMatcher.IsSubmitUrl,
                    extractor.GetMetaData,
                    extractor.ExtractFromHtml,
                    canonicalizeUrl: CbcGemUrlMatcher.CanonicalUrl);
            });
    }
}
