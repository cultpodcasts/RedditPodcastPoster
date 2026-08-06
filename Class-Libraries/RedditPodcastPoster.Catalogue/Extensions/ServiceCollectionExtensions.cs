using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;

namespace RedditPodcastPoster.Catalogue.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogueServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IEpisodeProvider, EpisodeProvider>()
            .AddSingleton<IFoundEpisodeFilter, FoundEpisodeFilter>()
            .AddScoped<IEpisodeResolver, EpisodeResolver>()
            .AddSingleton<IPodcastFilter, PodcastFilter>()
            .AddScoped<IPodcastFactory, PodcastFactory>();
    }
}
