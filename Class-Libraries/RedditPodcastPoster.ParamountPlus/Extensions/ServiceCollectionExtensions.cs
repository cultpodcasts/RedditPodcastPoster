using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.ParamountPlus.Extractors;
using RedditPodcastPoster.ParamountPlus.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.ParamountPlus.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddParamountPlusServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(ParamountPlusPageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IParamountPlusPageMetaDataExtractor, ParamountPlusPageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.ParamountPlus,
                    ServiceKeys.ParamountPlus,
                    ParamountPlusUrlMatcher.IsSubmitUrl,
                    ParamountPlusUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IParamountPlusPageMetaDataExtractor>().GetMetaData));
    }
}
