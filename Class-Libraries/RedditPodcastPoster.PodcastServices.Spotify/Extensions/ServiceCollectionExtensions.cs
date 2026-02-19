using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Configuration;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSpotifyClient()
        {
            return services
                .AddScoped<ISpotifyClientWrapper, SpotifyClientWrapper>()
                .AddScoped<ISpotifyClientFactory, SpotifyClientFactory>()
                .AddScoped<ISpotifyClientConfigFactory, SpotifyClientConfigFactory>()
                .AddScoped<IAsyncInstance<ISpotifyClient>>(s => 
                    new AsyncInstance<ISpotifyClient>(s.GetService<ISpotifyClientFactory>()!))
                .BindConfiguration<SpotifySettings>("spotify");
        }

        public IServiceCollection AddSpotifyServices()
        {
            return services
                .AddSpotifyClient()
                .AddScoped<ISpotifyEpisodeProvider, SpotifyEpisodeProvider>()
                .AddScoped<ISpotifyEpisodeEnricher, SpotifyEpisodeEnricher>()
                .AddScoped<ISpotifyPodcastEnricher, SpotifyPodcastEnricher>()
                .AddScoped<ISpotifyEpisodeResolver, SpotifyEpisodeResolver>()
                .AddScoped<ISpotifyPodcastEpisodesProvider, SpotifyPodcastEpisodesProvider>()
                .AddScoped<ISpotifyPodcastResolver, SpotifyPodcastResolver>()
                .AddScoped<ISpotifyQueryPaginator, SpotifyQueryPaginator>()
                .AddScoped<ISearchResultFinder, SearchResultFinder>()
                .AddScoped<ISpotifySearcher, SpotifySearcher>()
                .AddScoped<ISpotifyEpisodeRetrievalHandler, SpotifyEpisodeRetrievalHandler>();
        }
    }
}