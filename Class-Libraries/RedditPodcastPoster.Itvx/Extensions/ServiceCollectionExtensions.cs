using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Itvx.Extractors;
using RedditPodcastPoster.Itvx.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.Itvx.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddItvxServices(this IServiceCollection services)
    {
        // ITVX resets bare scrapes (UA + Accept only). Browser Accept-Language +
        // Sec-Fetch-* headers are required for a stable 200 with og:title.
        services.AddHttpClient(nameof(ItvxPageMetaDataExtractor), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd(
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-GB,en;q=0.9");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "none");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-User", "?1");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IItvxPageMetaDataExtractor, ItvxPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.Itvx,
                    ServiceKeys.Itvx,
                    ItvxUrlMatcher.IsSubmitUrl,
                    ItvxUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IItvxPageMetaDataExtractor>().GetMetaData));
    }
}
