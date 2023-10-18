using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.PodcastServices.Spotify.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSpotifyServices(this IServiceCollection services, IConfiguration config)
    {
        SpotifyClientFactory.AddSpotifyClient(services);

        services
            .AddOptions<SpotifySettings>().Bind(config.GetSection("spotify"));


        return services
            .AddScoped<ISpotifyEpisodeProvider, SpotifyEpisodeProvider>()
            .AddScoped<ISpotifyEpisodeEnricher, SpotifyEpisodeEnricher>()
            .AddScoped<ISpotifyPodcastEnricher, SpotifyPodcastEnricher>()
            .AddScoped<ISpotifyEpisodeResolver, SpotifyEpisodeResolver>()
            .AddScoped<ISpotifyPodcastResolver, SpotifyPodcastResolver>()
            .AddScoped<ISpotifyQueryPaginator, SpotifyQueryPaginator>()
            .AddScoped<ISpotifyClientWrapper, SpotifyClientWrapper>()
            .AddScoped<ISpotifySearcher, SpotifySearcher>();

    }
}