using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Channel4.Extractors;
using RedditPodcastPoster.Channel4.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.Channel4.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChannel4Services(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(Channel4PageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<IChannel4PageMetaDataExtractor, Channel4PageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.Channel4,
                    ServiceKeys.Channel4,
                    Channel4UrlMatcher.IsSubmitUrl,
                    Channel4UrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IChannel4PageMetaDataExtractor>().GetMetaData));
    }
}
