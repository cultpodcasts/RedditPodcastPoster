using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.DisneyPlus.Extractors;
using RedditPodcastPoster.DisneyPlus.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.DisneyPlus.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDisneyPlusServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(DisneyPlusPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IDisneyPlusPageMetaDataExtractor, DisneyPlusPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.DisneyPlus,
                    ServiceKeys.DisneyPlus,
                    DisneyPlusUrlMatcher.IsSubmitUrl,
                    DisneyPlusUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IDisneyPlusPageMetaDataExtractor>().GetMetaData));
    }
}
