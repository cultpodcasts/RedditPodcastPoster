using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.CanalPlus.Extractors;
using RedditPodcastPoster.CanalPlus.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.CanalPlus.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCanalPlusServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(CanalPlusPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<ICanalPlusPageMetaDataExtractor, CanalPlusPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    StreamingService.CanalPlus,
                    CanalPlusUrlMatcher.IsSubmitUrl,
                    CanalPlusUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<ICanalPlusPageMetaDataExtractor>().GetMetaData));
    }
}