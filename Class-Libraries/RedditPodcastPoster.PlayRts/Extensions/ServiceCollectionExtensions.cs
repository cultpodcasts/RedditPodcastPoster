using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PlayRts.Extractors;
using RedditPodcastPoster.PlayRts.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers; // pragma: allowlist secret

namespace RedditPodcastPoster.PlayRts.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlayRtsServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(PlayRtsPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IPlayRtsPageMetaDataExtractor, PlayRtsPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider => // pragma: allowlist secret
                new CatalogKeyedNonPodcastServiceAdapter( // pragma: allowlist secret
                    NonPodcastService.PlayRts, // pragma: allowlist secret
                    ServiceKeys.PlayRts,
                    PlayRtsUrlMatcher.IsSubmitUrl,
                    PlayRtsUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IPlayRtsPageMetaDataExtractor>().GetMetaData));
    }
}
