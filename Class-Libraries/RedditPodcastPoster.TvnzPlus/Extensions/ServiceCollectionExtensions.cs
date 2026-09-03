using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.TvnzPlus.Extractors;
using RedditPodcastPoster.TvnzPlus.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.TvnzPlus.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTvnzPlusServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(TvnzPlusPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<ITvnzPlusPageMetaDataExtractor, TvnzPlusPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.TvnzPlus,
                    ServiceKeys.TvnzPlus,
                    TvnzPlusUrlMatcher.IsSubmitUrl,
                    TvnzPlusUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<ITvnzPlusPageMetaDataExtractor>().GetMetaData));
    }
}
