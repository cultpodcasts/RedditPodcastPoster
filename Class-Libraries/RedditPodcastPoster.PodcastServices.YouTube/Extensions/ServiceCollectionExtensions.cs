using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYouTubeServices(this IServiceCollection services, IConfiguration config)
    {
        YouTubeServiceFactory.AddYouTubeService(services);

        services
            .AddOptions<YouTubeSettings>().Bind(config.GetSection("youtube"));

        return services
            .AddScoped<IYouTubeEpisodeProvider, YouTubeEpisodeProvider>()
            .AddScoped<IYouTubeEpisodeEnricher, YouTubeEpisodeEnricher>()
            .AddScoped<IYouTubeItemResolver, YouTubeItemResolver>()
            .AddScoped<IYouTubePlaylistService, YouTubePlaylistService>()
            .AddScoped<IYouTubeVideoService, YouTubeVideoService>()
            .AddScoped<IYouTubeChannelVideoSnippetsService, YouTubeChannelVideoSnippetsService>()
            .AddScoped<IYouTubeChannelService, YouTubeChannelService>()
            .AddSingleton<IYouTubeIdExtractor, YouTubeIdExtractor>()
            .AddScoped<ISearchResultFinder, SearchResultFinder>()
            .AddScoped<IYouTubeChannelResolver, YouTubeChannelResolver>()
            .AddScoped<IYouTubeSearcher, YouTubeSearcher>();
    }
}