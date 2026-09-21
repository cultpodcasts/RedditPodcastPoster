using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Ard.Extractors;
using RedditPodcastPoster.Ard.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Ard.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArdServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(ArdPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IArdPageMetaDataExtractor, ArdPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    StreamingService.Ard,
                    ArdUrlMatcher.IsSubmitUrl,
                    ArdUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IArdPageMetaDataExtractor>().GetMetaData));
    }
}