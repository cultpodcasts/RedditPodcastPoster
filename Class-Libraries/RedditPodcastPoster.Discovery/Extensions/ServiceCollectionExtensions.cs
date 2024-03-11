using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.ListenNotes.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Text.Extensions;

namespace RedditPodcastPoster.Discovery.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscovery(
        this IServiceCollection services,
        ConfigurationManager config)
    {
        return services
            .AddScoped<ISearchProvider, SearchProvider>()
            .AddScoped<ISpotifyEnrichingListenNotesSearcher, SpotifyEnrichingListenNotesSearcher>()
            .AddSpotifyServices(config)
            .AddYouTubeServices(config)
            .AddListenNotes(config)
            .AddTextSanitiser();
    }
}