using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.Vimeo.Extractors;
using RedditPodcastPoster.Vimeo.Matching;

namespace RedditPodcastPoster.Vimeo.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVimeoServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(VimeoMetaDataExtractor), client =>
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json"));

        return services
            .AddScoped<IVimeoMetaDataExtractor, VimeoMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.Vimeo,
                    ServiceKeys.Vimeo,
                    VimeoUrlMatcher.IsSubmitUrl,
                    VimeoUrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<IVimeoMetaDataExtractor>().GetMetaData));
    }
}
