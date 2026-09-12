using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.BcVideo.Extractors;
using RedditPodcastPoster.BcVideo.Matching;
using RedditPodcastPoster.Models.Pod\u0063asts;
using RedditPodcastPoster.Pod\u0063astServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.BcVideo.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBcVideoServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(BcVideoMetaDataExtractor), client =>
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json"));

        return services
            .AddScoped<IBcVideoMetaDataExtractor, BcVideoMetaDataExtractor>()
            .AddScoped<INonPod\u0063astServiceAdapter>(provider =>
                new CatalogKeyedNonPod\u0063astServiceAdapter(
                    NonPod\u0063astService.BcVideo,
                    ServiceKeys.BcVideo,
                    BcVideoUrlMatcher.IsSubmitUrl,
                    BcVideoUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IBcVideoMetaDataExtractor>().GetMetaData));
    }
}
