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
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
        });

        return services
            .AddScoped<IBcVideoMetaDataExtractor, BcVideoMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
            {
                var extractor = provider.GetRequiredService<IBcVideoMetaDataExtractor>();
                return new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.BcVideo,
                    ServiceKeys.BcVideo,
                    BcVideoUrlMatcher.IsSubmitUrl,
                    BcVideoUrlMatcher.IsSubmitUrl,
                    extractor.GetMetaData,
                    extractor.GetMetaData,
                    BcVideoUrlMatcher.CanonicalUrl);
            });
    }
}
