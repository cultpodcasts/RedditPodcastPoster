using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.TheTvdb.Extensions;
using RedditPodcastPoster.Tmdb.Extensions;

namespace RedditPodcastPoster.Catalogue.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogueServices(this IServiceCollection services)
    {
        return services
            .AddTmdbClient()
            .AddTheTvdbClient()
            .AddSingleton<ICatalogueAuthorityLookup, CatalogueAuthorityLookup>()
            .AddScoped<IEpisodeProvider, EpisodeProvider>()
            .AddSingleton<IFoundEpisodeFilter, FoundEpisodeFilter>()
            .AddScoped<IEpisodeResolver, EpisodeResolver>()
            .AddSingleton<IPodcastFilter, PodcastFilter>()
            .AddScoped<IPodcastFactory, PodcastFactory>();
    }
}
