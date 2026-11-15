using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Catalogue.Authority.Imdb;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.TheTvdb.Extensions;
using RedditPodcastPoster.Tmdb.Extensions;

namespace RedditPodcastPoster.Catalogue.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogueServices(this IServiceCollection services)
    {
        services.AddHttpClient<IImdbSuggestionClient, ImdbSuggestionClient>(client =>
        {
            client.BaseAddress = ImdbSuggestionClient.BaseAddress;
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("cultpodcasts-catalogue/1.0 (+https://cultpodcasts.com)");
        });
        return services
            .AddTmdbClient()
            .AddTheTvdbClient()
            .AddScoped<ICatalogueAuthorityLookup, CatalogueAuthorityLookup>()
            .AddScoped<ICatalogueAuthorityPreviewer, CatalogueAuthorityPreviewer>()
            .AddScoped<ICatalogueIdentityEnricher, CatalogueIdentityEnricher>()
            .AddScoped<IEpisodeProvider, EpisodeProvider>()
            .AddSingleton<IFoundEpisodeFilter, FoundEpisodeFilter>()
            .AddScoped<IEpisodeResolver, EpisodeResolver>()
            .AddSingleton<IPodcastFilter, PodcastFilter>()
            .AddScoped<IPodcastFactory, PodcastFactory>();
    }
}
