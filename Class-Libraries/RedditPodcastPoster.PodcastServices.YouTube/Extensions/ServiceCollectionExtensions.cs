using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYouTubeServices(this IServiceCollection services, ApplicationUsage usage)
    {
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
            .AddScoped<IYouTubeServiceFactory, YouTubeServiceFactory>()
            .AddScoped<IYouTubeApiKeyStrategy, YouTubeApiKeyStrategy>()
            .AddDateTimeService()
            .AddSingleton<IApplicationUsageProvider>(new ApplicationUsageProvider(usage))
            .AddScoped(s => s.GetService<IYouTubeServiceFactory>()!.Create(usage))
            .AddScoped<IYouTubeVideoServiceFactory, YouTubeVideoServiceFactory>()
            .AddScoped<ITolerantYouTubeChannelVideoSnippetsService, TolerantYouTubeChannelVideoSnippetsService>()
            .AddScoped<ICachedTolerantYouTubeChannelVideoSnippetsService,
                CachedTolerantYouTubeChannelVideoSnippetsService>();
    }
}