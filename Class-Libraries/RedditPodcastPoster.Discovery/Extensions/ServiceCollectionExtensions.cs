using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.ListenNotes.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Text.Extensions;

namespace RedditPodcastPoster.Discovery.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscovery(
        this IServiceCollection services, IConfiguration config)
    {
        return services
            .AddScoped<ISearchProvider, SearchProvider>()
            .AddScoped<IEpisodeResultsEnricher, EpisodeResultsEnricher>()
            .AddScoped<IEpisodeResultEnricher, EpisodeResultEnricher>()
            .AddScoped<ISpotifyEnricher, SpotifyEnricher>()
            .AddScoped<IAppleEnricher, AppleEnricher>()
            .AddScoped<IDiscoveryServiceConfigProvider, DiscoveryServiceConfigProvider>()
            .AddScoped<IDiscoveryService, DiscoveryService>()
            .AddScoped<IEnrichedEpisodeResultsAdapter, EnrichedEpisodeResultsAdapter>()
            .AddScoped<IEnrichedEpisodeResultAdapter, EnrichedEpisodeResultAdapter>()
            .AddScoped<IIgnoreTermsProvider, IgnoreTermsProvider>()
            .AddDiscoveryRepository(config)
            .AddSpotifyServices(config)
            .AddAppleServices()
            .AddYouTubeServices(config)
            .AddListenNotes(config)
            .AddTextSanitiser();
    }

    public static IServiceCollection AddDiscoveryRepository(
        this IServiceCollection services, IConfiguration config)
    {
        return services
            .AddScoped<IDiscoveryResultsRepository, DiscoveryResultsRepository>();
    }
}