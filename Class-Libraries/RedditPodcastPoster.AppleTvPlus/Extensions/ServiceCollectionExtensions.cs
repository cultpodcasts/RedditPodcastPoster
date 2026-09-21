using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.AppleTvPlus.Extractors;
using RedditPodcastPoster.AppleTvPlus.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.AppleTvPlus.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppleTvPlusServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(AppleTvPlusPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IAppleTvPlusPageMetaDataExtractor, AppleTvPlusPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    StreamingService.AppleTvPlus,
                    AppleTvPlusUrlMatcher.IsSubmitUrl,
                    AppleTvPlusUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IAppleTvPlusPageMetaDataExtractor>().GetMetaData));
    }
}