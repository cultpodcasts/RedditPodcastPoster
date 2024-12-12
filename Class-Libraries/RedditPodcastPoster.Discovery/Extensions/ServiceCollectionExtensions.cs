using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.ListenNotes.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Taddy.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Text.Extensions;

namespace RedditPodcastPoster.Discovery.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscovery(this IServiceCollection services, ApplicationUsage usage)
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
            .AddDiscoveryRepository()
            .AddSpotifyServices()
            .AddAppleServices()
            .AddYouTubeServices(usage).AddListenNotes()
            .AddTaddy()
            .AddTextSanitiser()
            .BindConfiguration<DiscoverySettings>("discover")
            s.BindConfiguration<IgnoreTermsSettings>("discover");
    }

    public static IServiceCollection AddDiscoveryRepository(this IServiceCollection services)
    {
        return services
            .AddScoped<IDiscoveryResultsRepository, DiscoveryResultsRepository>();
    }
}