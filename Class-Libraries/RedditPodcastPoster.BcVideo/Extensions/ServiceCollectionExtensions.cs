using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.BcVideo.Extractors;
using RedditPodcastPoster.BcVideo.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.BcVideo.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBcVideoServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(BcVideoMetaDataExtractor), client =>
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json"));

        return services
            .AddScoped<IBcVideoMetaDataExtractor, BcVideoMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.BcVideo,
                    ServiceKeys.BcVideo,
                    BcVideoUrlMatcher.IsSubmitUrl,
                    BcVideoUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IBcVideoMetaDataExtractor>().GetMetaData));
    }
}
