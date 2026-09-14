using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.FranceTv.Extractors;
using RedditPodcastPoster.FranceTv.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.FranceTv.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFranceTvServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(FranceTvPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IFranceTvPageMetaDataExtractor, FranceTvPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.FranceTv,
                    StreamingServiceKeys.FranceTv,
                    FranceTvUrlMatcher.IsSubmitUrl,
                    FranceTvUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IFranceTvPageMetaDataExtractor>().GetMetaData));
    }
}