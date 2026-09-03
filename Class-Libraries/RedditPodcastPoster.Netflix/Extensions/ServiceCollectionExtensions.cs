using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Netflix.Extractors;
using RedditPodcastPoster.Netflix.Matching;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.Netflix.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNetflixServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(NetflixPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<INetflixPageMetaDataExtractor, NetflixPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.Netflix,
                    ServiceKeys.Netflix,
                    NetflixUrlMatcher.IsSubmitUrl,
                    NetflixUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<INetflixPageMetaDataExtractor>().GetMetaData));
    }
}
