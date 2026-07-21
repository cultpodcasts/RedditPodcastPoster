using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Discovery.Adapters;
using RedditPodcastPoster.Discovery.Configuration;
using RedditPodcastPoster.Discovery.Enrichers;
using RedditPodcastPoster.Discovery.ML;
using RedditPodcastPoster.Discovery.Providers;
using RedditPodcastPoster.Discovery.Repositories;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.ListenNotes.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Taddy.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.Discovery.ML.Services;
using RedditPodcastPoster.Discovery.ML.Configuration;

namespace RedditPodcastPoster.Discovery.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDiscovery(ApplicationUsage usage)
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
                .AddSingleton<IDiscoveryResultScorer, DiscoveryResultScorer>()
                .AddSingleton<IDiscoveryResultDeduplicator, DiscoveryResultDeduplicator>()
                .AddDiscoveryRepository()
                .AddSpotifyServices()
                .AddAppleServices()
                .AddYouTubeServices(usage).AddListenNotes()
                .AddTaddy()
                .AddTextSanitiser()
                .BindConfiguration<DiscoverySettings>("discover")
                .BindConfiguration<IgnoreTermsSettings>("discover")
                .BindConfiguration<DiscoveryScorerSettings>("discover:scorer");
        }

        public IServiceCollection AddDiscoveryRepository()
        {
            return services
                .AddSingleton<IDiscoveryResultsRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DiscoveryResultsRepository>>();
                    return new DiscoveryResultsRepository(containerFactory.CreateDiscoveryContainer(), logger);
                });
        }
    }
}