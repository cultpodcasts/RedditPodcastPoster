using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PlaySuisse.Extractors;
using RedditPodcastPoster.PlaySuisse.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.PlaySuisse.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlaySuisseServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(PlaySuissePageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IPlaySuissePageMetaDataExtractor, PlaySuissePageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.PlaySuisse,
                    ServiceKeys.PlaySuisse,
                    PlaySuisseUrlMatcher.IsSubmitUrl,
                    PlaySuisseUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IPlaySuissePageMetaDataExtractor>().GetMetaData));
    }
}
