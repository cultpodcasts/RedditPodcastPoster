using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYouTubeServices(this IServiceCollection services)
    {
        YouTubeServiceFactory.AddYouTubeService(services);

        services.BindConfiguration<YouTubeSettings>("youtube");

        return services
                .AddScoped<IYouTubeEpisodeProvider, YouTubeEpisodeProvider>()
                .AddScoped<IYouTubeEpisodeEnricher, YouTubeEpisodeEnricher>()
                .AddScoped<IYouTubeItemResolver, YouTubeItemResolver>()
                .AddScoped<IYouTubePlaylistService, YouTubePlaylistService>()
                .AddScoped<IYouTubeVideoService, YouTubeVideoService>()
                .AddScoped<IYouTubeChannelVideoSnippetsService, YouTubeChannelVideoSnippetsService>()
                .AddScoped<IYouTubeChannelService, YouTubeChannelService>()
                .AddScoped<ISearchResultFinder, SearchResultFinder>()
                .AddScoped<IYouTubeChannelResolver, YouTubeChannelResolver>()
                .AddScoped<IYouTubeSearcher, YouTubeSearcher>()
                .AddSingleton<INoRedirectHttpClientFactory, NoRedirectHttpClientFactory>()
                .AddScoped<IYouTubeEpisodeRetrievalHandler, YouTubeEpisodeRetrievalHandler>()
                .AddScoped<IYouTubeChannelVideosService, YouTubeChannelVideosService>()
            ;
    }
}