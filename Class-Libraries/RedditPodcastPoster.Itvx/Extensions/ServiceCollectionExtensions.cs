using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Itvx.Extractors;
using RedditPodcastPoster.Itvx.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.Itvx.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddItvxServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(ItvxPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IItvxPageMetaDataExtractor, ItvxPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.Itvx,
                    ServiceKeys.Itvx,
                    ItvxUrlMatcher.IsSubmitUrl,
                    ItvxUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IItvxPageMetaDataExtractor>().GetMetaData));
    }
}
